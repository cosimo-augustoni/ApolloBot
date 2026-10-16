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
/// Discord drops a response that comes later than 3 s. When the router has not replied after
/// <see cref="DeferAfter"/>, the response is deferred (ephemerally) and edited once the reply is there. Deferral is a
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
    /// How long a handler may take before the response is deferred; Discord drops responses after 3 s.
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

        try
        {
            await RespondAsync(interaction, route());
        }
        catch (Exception exception)
        {
            LogResponseFailed(exception, interaction.GetType().Name, interaction.Id);
        }
    }

    private async Task RespondAsync(Interaction interaction, Task<CommandReply> replying)
    {
        using var deferral = new CancellationTokenSource();
        var deadline = Task.Delay(DeferAfter, timeProvider, deferral.Token);
        if (await Task.WhenAny(replying, deadline) == replying)
        {
            await deferral.CancelAsync();
            await interaction.SendResponseAsync(InteractionCallback.Message(DiscordReplies.ToMessage(await replying)));
            return;
        }

        LogDeferred(interaction.GetType().Name, interaction.Id, DeferAfter);
        await interaction.SendResponseAsync(InteractionCallback.DeferredMessage(DiscordReplies.DeferredFlags));
        var reply = await replying;
        await interaction.ModifyResponseAsync(message => DiscordReplies.Apply(message, reply));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deferred the response to {InteractionType} {InteractionId}: no reply after {DeferAfter}")]
    private partial void LogDeferred(string interactionType, ulong interactionId, TimeSpan deferAfter);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not respond to {InteractionType} {InteractionId}")]
    private partial void LogResponseFailed(Exception exception, string interactionType, ulong interactionId);
}
