using Apollo.Platform.Abstractions.Commands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord;

/// <summary>
/// Registers the <see cref="CommandDefinition"/>s as slash commands of the configured guild once the gateway is ready.
/// Guild-scoped only (scope §4.3): guild commands update at once, and the bot serves no other guild.
/// </summary>
/// <remarks>
/// The registration overwrites the guild's commands, so a command removed from the code disappears too. It runs on
/// every READY (not on a resumed session), which is idempotent. A failure is logged; the bot keeps recording and the
/// commands from the last successful registration stay in place.
/// </remarks>
internal sealed partial class DiscordCommandRegistrar(
    GatewayClient gateway,
    IEnumerable<CommandDefinition> definitions,
    IOptions<DiscordPlatformOptions> options,
    ILogger<DiscordCommandRegistrar> logger) : IReadyGatewayHandler
{
    private readonly ulong _guildId = options.Value.GuildId
        ?? throw new InvalidOperationException($"{DiscordPlatformOptions.SectionName}:GuildId is not configured.");

    public async ValueTask HandleAsync(ReadyEventArgs arg)
    {
        ArgumentNullException.ThrowIfNull(arg);

        try
        {
            var commands = CommandDefinition.Combine(definitions);
            await gateway.Rest.BulkOverwriteGuildApplicationCommandsAsync(
                arg.ApplicationId, _guildId, [.. commands.Select(DiscordCommands.ToProperties)]);
            LogRegistered(commands.Count, _guildId);
        }
        catch (Exception exception)
        {
            LogRegistrationFailed(exception, _guildId);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered {CommandCount} slash commands for guild {GuildId}")]
    private partial void LogRegistered(int commandCount, ulong guildId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not register the slash commands for guild {GuildId}")]
    private partial void LogRegistrationFailed(Exception exception, ulong guildId);
}
