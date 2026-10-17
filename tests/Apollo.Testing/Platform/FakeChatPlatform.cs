using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Commands;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

/// <summary>
/// In-memory <see cref="IChatPlatform"/> for recorder tests: seed guild state with the <c>With*</c> methods, raise
/// events with <see cref="RaiseAsync"/> (through the real <see cref="IPlatformEventDispatcher"/>, like an adapter), and
/// assert on <see cref="RoleChanges"/>. With <see cref="WithCommandRouter"/> it also runs commands and buttons and
/// records the <see cref="Replies"/>.
/// </summary>
/// <example>
/// <code>
/// var platform = new FakeChatPlatform(dispatcher, TestTime.CreateProvider())
///     .WithMember(new PlatformMemberBuilder().ForUser(alice).WithRoles(trackingRole).Build());
/// await platform.RaiseAsync(new PlatformMessageCreatedBuilder().ByAuthor(alice).Build(), cancellationToken);
/// </code>
/// </example>
/// <param name="dispatcher">Delivers raised events to the registered handlers.</param>
/// <param name="timeProvider">Stamps the echoed <see cref="PlatformMemberUpdated"/> events.</param>
/// <param name="guild">The one guild this fake serves; defaults to <see cref="TestPlatform.Guild"/>.</param>
public sealed class FakeChatPlatform(
    IPlatformEventDispatcher dispatcher,
    TimeProvider timeProvider,
    PlatformRef? guild = null) : IChatPlatform
{
    private readonly Lock _lock = new();
    private readonly List<PlatformMember> _members = [];
    private readonly List<PlatformRole> _roles = [];
    private readonly List<PlatformChannel> _channels = [];
    private readonly List<PlatformVoiceState> _voiceStates = [];
    private readonly List<PlatformPresence> _presences = [];
    private readonly List<FakeRoleChange> _roleChanges = [];
    private readonly List<FakeReply> _replies = [];
    private ICommandRouter? _commandRouter;
    private Exception? _roleChangeFailure;
    private int _memberListingFailures;
    private int _memberListings;

    public PlatformRef Guild { get; } = guild ?? TestPlatform.Guild;

    public PlatformKind Platform => Guild.Platform;

    /// <summary>
    /// Presence activities are supported by default (like Discord); turn them off with <see cref="WithCapabilities"/>.
    /// </summary>
    public PlatformCapabilities Capabilities { get; private set; } = new(PresenceActivities: true);

    /// <summary>
    /// Every <see cref="AddRoleAsync"/> and <see cref="RemoveRoleAsync"/> call, in order.
    /// </summary>
    public IReadOnlyList<FakeRoleChange> RoleChanges
    {
        get
        {
            lock (_lock)
            {
                return [.. _roleChanges];
            }
        }
    }

    /// <summary>
    /// Every reply delivered after a command or button, in order.
    /// </summary>
    public IReadOnlyList<FakeReply> Replies
    {
        get
        {
            lock (_lock)
            {
                return [.. _replies];
            }
        }
    }

    /// <summary>
    /// How often <see cref="ListMembersAsync"/> was called, failed calls included.
    /// </summary>
    public int MemberListings => Volatile.Read(ref _memberListings);

    /// <summary>
    /// Makes the next <paramref name="times"/> <see cref="ListMembersAsync"/> calls throw a <see cref="TimeoutException"/>,
    /// as a member download that never completes would.
    /// </summary>
    public FakeChatPlatform FailMemberListings(int times)
    {
        Volatile.Write(ref _memberListingFailures, times);
        return this;
    }

    /// <summary>
    /// Makes every following <see cref="AddRoleAsync"/> and <see cref="RemoveRoleAsync"/> call throw
    /// <paramref name="exception"/> without changing or recording anything, as a platform that refuses the change
    /// (missing permission, role hierarchy) would.
    /// </summary>
    public FakeChatPlatform FailRoleChanges(Exception exception)
    {
        lock (_lock)
        {
            _roleChangeFailure = exception;
        }

        return this;
    }

    public FakeChatPlatform WithCapabilities(PlatformCapabilities capabilities)
    {
        Capabilities = capabilities;
        return this;
    }

    /// <summary>
    /// Adds the member, replacing an existing member with the same user.
    /// </summary>
    public FakeChatPlatform WithMember(PlatformMember member) => Upsert(_members, member, m => m.User == member.User);

    /// <summary>
    /// Adds the role, replacing an existing role with the same reference.
    /// </summary>
    public FakeChatPlatform WithRole(PlatformRole role) => Upsert(_roles, role, r => r.Role == role.Role);

    /// <summary>
    /// Adds the channel, replacing an existing channel with the same reference.
    /// </summary>
    public FakeChatPlatform WithChannel(PlatformChannel channel) => Upsert(_channels, channel, c => c.Channel == channel.Channel);

    /// <summary>
    /// Adds a voice connection, replacing the one with the same user and <see cref="PlatformVoiceState.ConnectionId"/>.
    /// Use distinct connection IDs to give one user several connections (as on Fluxer).
    /// </summary>
    public FakeChatPlatform WithVoiceState(PlatformVoiceState voiceState) =>
        Upsert(_voiceStates, voiceState, v => v.User == voiceState.User && v.ConnectionId == voiceState.ConnectionId);

    /// <summary>
    /// Adds the presence, replacing an existing presence of the same user.
    /// </summary>
    public FakeChatPlatform WithPresence(PlatformPresence presence) => Upsert(_presences, presence, p => p.User == presence.User);

    /// <summary>
    /// Delivers <paramref name="platformEvent"/> to the registered handlers, the way an adapter does.
    /// </summary>
    public Task RaiseAsync(PlatformEvent platformEvent, CancellationToken cancellationToken) =>
        dispatcher.DispatchAsync(platformEvent, cancellationToken);

    /// <summary>
    /// Lets <see cref="InvokeCommandAsync(PlatformRef, string, IReadOnlyDictionary{string, object?}, CancellationToken)"/>
    /// and <see cref="ClickButtonAsync"/> deliver to <paramref name="router"/>, the way an adapter does.
    /// </summary>
    public FakeChatPlatform WithCommandRouter(ICommandRouter router)
    {
        _commandRouter = router;
        return this;
    }

    /// <summary>
    /// <paramref name="member"/> runs the command <paramref name="path"/> in <see cref="Guild"/>; the reply is recorded
    /// in <see cref="Replies"/> and returned.
    /// </summary>
    public Task<CommandReply> InvokeCommandAsync(
        PlatformRef member,
        string path,
        IReadOnlyDictionary<string, object?> options,
        CancellationToken cancellationToken) =>
        ReplyAsync(
            member,
            router => router.HandleCommandAsync(new CommandInvocation(Guild, member, path, options, timeProvider.GetUtcNow()), cancellationToken));

    /// <inheritdoc cref="InvokeCommandAsync(PlatformRef, string, IReadOnlyDictionary{string, object?}, CancellationToken)"/>
    public Task<CommandReply> InvokeCommandAsync(PlatformRef member, string path, CancellationToken cancellationToken) =>
        InvokeCommandAsync(member, path, new Dictionary<string, object?>(), cancellationToken);

    /// <summary>
    /// <paramref name="member"/> uses the button <paramref name="buttonId"/> in <see cref="Guild"/>; the reply is
    /// recorded in <see cref="Replies"/> and returned.
    /// </summary>
    public Task<CommandReply> ClickButtonAsync(PlatformRef member, string buttonId, CancellationToken cancellationToken) =>
        ReplyAsync(
            member,
            router => router.HandleButtonAsync(new ButtonInvocation(Guild, member, buttonId, timeProvider.GetUtcNow()), cancellationToken));

    public Task<PlatformMember?> GetMemberAsync(PlatformRef guild, PlatformRef user, CancellationToken cancellationToken) =>
        Task.FromResult(FindMember(guild, user));

    public Task<IReadOnlyList<PlatformRef>> GetMemberRolesAsync(PlatformRef guild, PlatformRef user, CancellationToken cancellationToken) =>
        Task.FromResult(FindMember(guild, user)?.Roles ?? []);

    public Task<PlatformPermissions> GetMemberPermissionsAsync(PlatformRef guild, PlatformRef user, CancellationToken cancellationToken) =>
        Task.FromResult(FindMember(guild, user)?.IsAdministrator == true ? PlatformPermissions.Administrator : PlatformPermissions.None);

    public Task<IReadOnlyList<PlatformRole>> ListRolesAsync(PlatformRef guild, CancellationToken cancellationToken) =>
        List(guild, _roles);

    public Task<IReadOnlyList<PlatformChannel>> ListChannelsAsync(PlatformRef guild, CancellationToken cancellationToken) =>
        List(guild, _channels);

    /// <summary>
    /// The seeded members, unless <see cref="FailMemberListings"/> is still making calls fail.
    /// </summary>
    public Task<IReadOnlyList<PlatformMember>> ListMembersAsync(PlatformRef guild, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _memberListings);
        return Interlocked.Decrement(ref _memberListingFailures) >= 0
            ? Task.FromException<IReadOnlyList<PlatformMember>>(new TimeoutException("The fake member listing was set to fail."))
            : List(guild, _members);
    }

    public Task<IReadOnlyList<PlatformVoiceState>> ListVoiceStatesAsync(PlatformRef guild, CancellationToken cancellationToken) =>
        List(guild, _voiceStates);

    /// <summary>
    /// The seeded presences; without <see cref="PlatformCapabilities.PresenceActivities"/> their activities are dropped,
    /// as a platform without activities would report them.
    /// </summary>
    public async Task<IReadOnlyList<PlatformPresence>> ListPresencesAsync(PlatformRef guild, CancellationToken cancellationToken)
    {
        var presences = await List(guild, _presences);
        return Capabilities.PresenceActivities ? presences : [.. presences.Select(p => p with { Activities = [] })];
    }

    /// <summary>
    /// Records the call, gives the member the role and echoes a <see cref="PlatformMemberUpdated"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user is not a seeded member.</exception>
    public Task AddRoleAsync(PlatformRef guild, PlatformRef user, PlatformRef role, CancellationToken cancellationToken) =>
        ChangeRoleAsync(guild, new FakeRoleChange(user, role, FakeRoleChangeKind.Added), cancellationToken);

    /// <summary>
    /// Records the call, takes the role from the member and echoes a <see cref="PlatformMemberUpdated"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user is not a seeded member.</exception>
    public Task RemoveRoleAsync(PlatformRef guild, PlatformRef user, PlatformRef role, CancellationToken cancellationToken) =>
        ChangeRoleAsync(guild, new FakeRoleChange(user, role, FakeRoleChangeKind.Removed), cancellationToken);

    private async Task ChangeRoleAsync(PlatformRef guild, FakeRoleChange change, CancellationToken cancellationToken)
    {
        PlatformMember updated;
        lock (_lock)
        {
            if (_roleChangeFailure is { } failure)
            {
                throw failure;
            }

            var member = FindMember(guild, change.User)
                ?? throw new InvalidOperationException($"{change.User} is not a member of {Guild}.");
            IReadOnlyList<PlatformRef> roles = change.Kind == FakeRoleChangeKind.Added
                ? [.. member.Roles.Where(r => r != change.Role), change.Role]
                : [.. member.Roles.Where(r => r != change.Role)];
            updated = member with { Roles = roles };
            _members[_members.IndexOf(member)] = updated;
            _roleChanges.Add(change);
        }

        await RaiseAsync(new PlatformMemberUpdated(Guild, timeProvider.GetUtcNow(), updated), cancellationToken);
    }

    private async Task<CommandReply> ReplyAsync(PlatformRef member, Func<ICommandRouter, Task<CommandReply>> route)
    {
        var router = _commandRouter
            ?? throw new InvalidOperationException($"Call {nameof(WithCommandRouter)} before running commands or buttons.");
        var reply = await route(router);
        lock (_lock)
        {
            _replies.Add(new FakeReply(member, reply));
        }

        return reply;
    }

    private PlatformMember? FindMember(PlatformRef guild, PlatformRef user)
    {
        EnsureGuild(guild);
        lock (_lock)
        {
            return _members.Find(m => m.User == user);
        }
    }

    private Task<IReadOnlyList<T>> List<T>(PlatformRef guild, List<T> items)
    {
        EnsureGuild(guild);
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<T>>([.. items]);
        }
    }

    private FakeChatPlatform Upsert<T>(List<T> items, T item, Predicate<T> isSame)
    {
        lock (_lock)
        {
            var index = items.FindIndex(isSame);
            if (index >= 0)
            {
                items[index] = item;
            }
            else
            {
                items.Add(item);
            }
        }

        return this;
    }

    /// <summary>
    /// Fails loudly when a test passes another guild, instead of answering with empty state.
    /// </summary>
    private void EnsureGuild(PlatformRef guild)
    {
        if (guild != Guild)
        {
            throw new InvalidOperationException($"This fake serves {Guild}, not {guild}.");
        }
    }
}
