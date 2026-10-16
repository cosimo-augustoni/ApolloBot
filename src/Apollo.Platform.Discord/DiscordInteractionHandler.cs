using Apollo.Platform.Abstractions.Commands;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Rest;

namespace Apollo.Platform.Discord;

/// <summary>
/// Delivers slash commands and button clicks of the configured guild to <see cref="ICommandRouter"/> and sends its
/// reply as the interaction response. Interactions from other guilds and DMs are dropped by the single-guild filter.
/// </summary>
/// <remarks>
/// Discord drops a response that comes later than 3 s after the interaction was created. When the router has not
/// replied <see cref="DeferAfter"/> after that, the response is deferred (ephemerally) and edited once the reply is
/// there. A mapping or routing failure is answered with <see cref="ICommandRouter.CreateErrorReply"/>. Deferral is a
/// Discord concept and stays here; the neutral model knows nothing of it. NetCord's gateway handlers carry no
/// cancellation token, so routing is not cancellable.
/// </remarks>
internal sealed partial class DiscordInteractionHandler(
    DiscordEventMapper mapper,
    ICommandRouter router,
    TimeProvider timeProvider,
    ILogger<DiscordInteractionHandler> logger) : IInteractionCreateGatewayHandler
{
    /// <summary>
    /// How long after the interaction's creation the response is deferred if no reply is there; Discord drops it after 3 s.
    /// </summary>
    public static readonly TimeSpan DeferAfter = TimeSpan.FromSeconds(2);

    public async ValueTask HandleAsync(Interaction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        Func<Task<CommandReply>>? route = interaction switch
        {
            SlashCommandInteraction slash => () =>
                router.HandleCommandAsync(DiscordInteractions.ToInvocation(slash, mapper.Guild), CancellationToken.None),
            ButtonInteraction button => () =>
                router.HandleButtonAsync(DiscordInteractions.ToInvocation(button, mapper.Guild), CancellationToken.None),
            _ => null,
        };

        if (route is null || !mapper.IsConfiguredGuild(nameof(GatewayClient.InteractionCreate), interaction.GuildId))
        {
            return;
        }

        // The clock starts before routing, and counts from the interaction's creation: Discord's 3 s do too.
        var deferIn = DeferDelay(interaction.CreatedAt);

        // Off the gateway thread, so a router that blocks synchronously (or a failing mapping) cannot hold up deferral.
        var replying = Task.Run(route);

        try
        {
            await RespondAsync(interaction, replying, deferIn);
        }
        catch (Exception exception)
        {
            LogResponseFailed(exception, interaction.GetType().Name, interaction.Id);
        }
    }

    private async Task RespondAsync(Interaction interaction, Task<CommandReply> replying, TimeSpan deferIn)
    {
        using var deferral = new CancellationTokenSource();
        var deadline = Task.Delay(deferIn, timeProvider, deferral.Token);
        if (await Task.WhenAny(replying, deadline) == replying)
        {
            await deferral.CancelAsync();
            var reply = await ReplyOrErrorAsync(interaction, replying);
            await interaction.SendResponseAsync(InteractionCallback.Message(DiscordReplies.ToMessage(reply)));
            return;
        }

        LogDeferred(interaction.GetType().Name, interaction.Id, DeferAfter);
        await interaction.SendResponseAsync(InteractionCallback.DeferredMessage(DiscordReplies.DeferredFlags));
        var lateReply = await ReplyOrErrorAsync(interaction, replying);
        await interaction.ModifyResponseAsync(message => DiscordReplies.Apply(message, lateReply));
    }

    /// <summary>
    /// The router's reply, or its generic error reply when mapping or routing failed: the member always gets an answer.
    /// </summary>
    private async Task<CommandReply> ReplyOrErrorAsync(Interaction interaction, Task<CommandReply> replying)
    {
        try
        {
            return await replying;
        }
        catch (Exception exception)
        {
            LogRoutingFailed(exception, interaction.GetType().Name, interaction.Id);
            return router.CreateErrorReply();
        }
    }

    // Never longer than DeferAfter, so a local clock behind Discord's cannot push the response past the deadline.
    private TimeSpan DeferDelay(DateTimeOffset createdAt)
    {
        var remaining = createdAt + DeferAfter - timeProvider.GetUtcNow();
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining > DeferAfter ? DeferAfter : remaining;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not route {InteractionType} {InteractionId}; answering with the error reply")]
    private partial void LogRoutingFailed(Exception exception, string interactionType, ulong interactionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deferred the response to {InteractionType} {InteractionId}: no reply after {DeferAfter}")]
    private partial void LogDeferred(string interactionType, ulong interactionId, TimeSpan deferAfter);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not respond to {InteractionType} {InteractionId}")]
    private partial void LogResponseFailed(Exception exception, string interactionType, ulong interactionId);
}
