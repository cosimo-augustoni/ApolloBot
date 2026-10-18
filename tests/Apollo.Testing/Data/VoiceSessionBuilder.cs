using System.Globalization;
using Apollo.Data.Voice;
using Apollo.Domain.Voice;

namespace Apollo.Testing.Data;

/// <summary>
/// Builds a <see cref="VoiceSession"/> row: by default an open session started at <see cref="TestTime.Now"/>.
/// <c>Until</c> closes it as <see cref="VoiceSessionEndReason.Left"/>. Guild, user and channel are
/// internal IDs; seed them first.
/// </summary>
public sealed class VoiceSessionBuilder : TestDataBuilder<VoiceSessionBuilder, VoiceSession>
{
    public VoiceSessionBuilder In(Guid guildId, Guid channelId) => With(s => Copy(s, guildId: guildId, channelId: channelId));

    public VoiceSessionBuilder By(Guid userId) => With(s => Copy(s, userId: userId));

    public VoiceSessionBuilder From(DateTimeOffset startedAt) => With(s => Copy(s, startedAt: startedAt));

    public VoiceSessionBuilder From(string startedAtUtc) => From(Parse(startedAtUtc));

    public VoiceSessionBuilder Until(DateTimeOffset endedAt, VoiceSessionEndReason reason = VoiceSessionEndReason.Left) =>
        With(s => Copy(s, endedAt: endedAt, endReason: reason));

    public VoiceSessionBuilder Until(string endedAtUtc) => Until(Parse(endedAtUtc));

    protected override VoiceSession CreateDefault() => new() { OccurredAt = TestTime.Now };

    private static DateTimeOffset Parse(string utc) => DateTimeOffset.Parse(utc, CultureInfo.InvariantCulture);

    private static VoiceSession Copy(
        VoiceSession s,
        Guid? guildId = null,
        Guid? userId = null,
        Guid? channelId = null,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? endedAt = null,
        VoiceSessionEndReason? endReason = null) => new()
        {
            GuildId = guildId ?? s.GuildId,
            UserId = userId ?? s.UserId,
            ChannelId = channelId ?? s.ChannelId,
            OccurredAt = startedAt ?? s.OccurredAt,
            EndedAt = endedAt ?? s.EndedAt,
            EndReason = endReason ?? s.EndReason,
        };
}
