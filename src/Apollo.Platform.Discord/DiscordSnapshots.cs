using System.Globalization;
using Apollo.Platform.Abstractions.Snapshots;
using NetCord;
using NetCord.Gateway;

namespace Apollo.Platform.Discord;

/// <summary>
/// Maps NetCord's cached guild state to the neutral snapshots. Shared by <see cref="DiscordChatPlatform"/> and the
/// event mappers that carry snapshots (#13, #16, #18, #19).
/// </summary>
internal static class DiscordSnapshots
{
    /// <param name="user">The member.</param>
    /// <param name="guild">The cached guild, for the member's permissions; without it the member is not flagged as administrator.</param>
    public static PlatformMember ToMember(GuildUser user, Guild? guild) => new(
        User: DiscordIds.ToRef(user.Id),
        DisplayName: user.Nickname ?? user.GlobalName ?? user.Username,
        AvatarUrl: AvatarUrl(user),
        IsBot: user.IsBot,
        IsAdministrator: guild is not null && user.GetPermissions(guild).HasFlag(Permissions.Administrator),
        JoinedAt: user.JoinedAt,
        Roles: [.. user.RoleIds.Select(DiscordIds.ToRef)]);

    public static PlatformRole ToRole(Role role) => new(
        Role: DiscordIds.ToRef(role.Id),
        Name: role.Name,
        Position: role.RawPosition,
        IsAdministrator: role.Permissions.HasFlag(Permissions.Administrator),
        ManagesRoles: role.Permissions.HasFlag(Permissions.ManageRoles),
        IsManaged: role.Managed,
        Color: role.Colors.PrimaryColor.RawValue is var color and not 0 ? color : null);

    /// <summary>
    /// @everyone shares the guild's ID and is held by every member implicitly, so the adapter never reports it.
    /// </summary>
    public static bool IsEveryone(Role role) => role.Id == role.GuildId;

    public static IEnumerable<Role> RolesWithoutEveryone(Guild guild) => guild.Roles.Values.Where(role => !IsEveryone(role));

    public static PlatformChannel ToChannel(IGuildChannel channel) => new(
        Channel: DiscordIds.ToRef(channel.Id),
        Name: channel.Name,
        Type: ChannelType(channel),
        Category: Category(channel) is { } categoryId ? DiscordIds.ToRef(categoryId) : null,
        ThreadParent: channel is GuildThread thread ? DiscordIds.ToRef(thread.ParentId) : null);

    /// <summary>
    /// Discord has one voice state per user, so <see cref="PlatformVoiceState.ConnectionId"/> stays empty.
    /// </summary>
    public static PlatformVoiceState ToVoiceState(VoiceState state) => new(
        User: DiscordIds.ToRef(state.UserId),
        Channel: state.ChannelId is { } channelId ? DiscordIds.ToRef(channelId) : null,
        ConnectionId: null,
        SelfMute: state.IsSelfMuted,
        SelfDeaf: state.IsSelfDeafened,
        Streaming: state.SelfStreamExists ?? false,
        Video: state.SelfVideoExists);

    public static PlatformPresence ToPresence(Presence presence) => new(
        User: DiscordIds.ToRef(presence.User.Id),
        Status: presence.Status switch
        {
            UserStatusType.Online => PlatformPresenceStatus.Online,
            UserStatusType.Idle => PlatformPresenceStatus.Idle,
            UserStatusType.DoNotDisturb => PlatformPresenceStatus.DoNotDisturb,
            _ => PlatformPresenceStatus.Offline,
        },
        Activities: [.. presence.Activities.Select(ToActivity)]);

    private static PlatformActivity ToActivity(UserActivity activity) => new(
        Type: activity.Type switch
        {
            UserActivityType.Streaming => PlatformActivityType.Streaming,
            UserActivityType.Listening => PlatformActivityType.Listening,
            UserActivityType.Watching => PlatformActivityType.Watching,
            UserActivityType.Custom => PlatformActivityType.Custom,
            UserActivityType.Competing => PlatformActivityType.Competing,
            _ => PlatformActivityType.Playing,
        },
        Name: activity.Name,
        ReportedStart: activity.Timestamps?.StartTime)
    {
        // NetCord 1.0.0-beta.28 does not expose sync_id (Spotify's track ID), so it is not mapped (#115).
        ReportedEnd = activity.Timestamps?.EndTime,
        Details = activity.Details,
        State = activity.State,
        LargeText = activity.Assets?.LargeText,
        SmallText = activity.Assets?.SmallText,
        PartyId = activity.Party?.Id,
        PartySize = activity.Party?.Size?.CurrentSize,
        PartyMax = activity.Party?.Size?.MaxSize,
        ApplicationId = activity.ApplicationId?.ToString(CultureInfo.InvariantCulture),
    };

    // In NetCord threads, announcement, voice and stage channels all derive from TextGuildChannel: most specific first.
    private static PlatformChannelType ChannelType(IGuildChannel channel) => channel switch
    {
        GuildThread => PlatformChannelType.Thread,
        CategoryGuildChannel => PlatformChannelType.Category,
        AnnouncementGuildChannel => PlatformChannelType.Announcement,
        StageGuildChannel => PlatformChannelType.Stage,
        VoiceGuildChannel => PlatformChannelType.Voice,
        TextGuildChannel => PlatformChannelType.Text,
        ForumGuildChannel => PlatformChannelType.Forum,
        _ => PlatformChannelType.Other,
    };

    // A thread's parent is its channel, not a category (that is ThreadParent).
    private static ulong? Category(IGuildChannel channel) => channel switch
    {
        GuildThread => null,
        TextGuildChannel text => text.ParentId,
        ForumGuildChannel forum => forum.ParentId,
        _ => null,
    };

    private static string? AvatarUrl(GuildUser user) =>
        (user.HasGuildAvatar ? user.GetGuildAvatarUrl() : user.GetAvatarUrl())?.ToString();
}
