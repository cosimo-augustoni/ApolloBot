using Apollo.Bot.Resources;
using Apollo.Data.Identity;
using Apollo.Platform.Abstractions.Commands;
using Microsoft.Extensions.Localization;

namespace Apollo.Bot.Tracking;

/// <summary>
/// <c>/apollo tracking status</c> (scope §5.4): whether the gate tracks the member right now, since when (the start of
/// the open <c>tracking_periods</c> row), and how many rows per event table are stored, from all periods. Mentions when
/// tracking is not set up. Creates no data: a member the bot has never seen is untracked and has nothing stored.
/// </summary>
internal sealed class TrackingStatusCommandHandler(
    ITrackingGate gate,
    IPlatformIdResolver ids,
    TrackingHistory history,
    IUserDataSummary summary,
    IStringLocalizer<Strings> strings) : ICommandHandler
{
    public string Path => "apollo tracking status";

    public async Task<CommandReply> HandleAsync(CommandInvocation invocation, CancellationToken cancellationToken)
    {
        var guildId = await ids.TryGetGuildAsync(invocation.Guild, cancellationToken);
        var userId = await ids.TryGetUserAsync(invocation.Member, cancellationToken);

        // One view for the whole reply, so the tracked state and the setup hint come from the same snapshot.
        var view = gate.Current;
        var tracked = false;
        DateTimeOffset? since = null;
        var counts = UserDataCounts.None;
        if (guildId is { } guild && userId is { } user)
        {
            tracked = view.IsTracked(user);
            since = tracked ? await history.OpenPeriodStartAsync(guild, user, cancellationToken) : null;
            counts = await summary.CountAsync(guild, user, cancellationToken);
        }

        return CommandReply.Private($"{Tracking(view, tracked, since)}\n\n{Data(counts)}");
    }

    /// <summary>
    /// The first paragraph: tracked since when or not, then why not or how to start.
    /// </summary>
    private string Tracking(ITrackingView view, bool tracked, DateTimeOffset? since)
    {
        string state = (tracked, since) switch
        {
            (true, { } from) => strings["Status_TrackedSince", ReplyFormat.Date(from)],
            (true, null) => strings["Status_Tracked"],
            _ => strings["Status_NotTracked"],
        };

        string? next = view.TrackingRole is not null
            ? (tracked ? null : strings["Status_TurnOnHint"].Value)
            : strings["Tracking_NotSetUp"].Value;

        return next is null ? state : $"{state} {next}";
    }

    /// <summary>
    /// The second paragraph: the counts per event table.
    /// </summary>
    private string Data(UserDataCounts counts) =>
        counts.Total == 0
            ? strings["Status_NoData"]
            : string.Join(
                "\n",
                strings["Status_DataHeading"],
                strings["Status_Messages", ReplyFormat.Number(counts.Messages)],
                strings["Status_Reactions", ReplyFormat.Number(counts.Reactions)],
                strings["Status_VoiceSessions", ReplyFormat.Number(counts.VoiceSessions)],
                strings["Status_PresenceSessions", ReplyFormat.Number(counts.PresenceSessions)],
                strings["Status_MembershipEvents", ReplyFormat.Number(counts.MembershipEvents)]);
}
