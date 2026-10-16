using System.Collections.Concurrent;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord;

/// <summary>
/// Requests a guild's full member list over the gateway (REQUEST_GUILD_MEMBERS) and collects the GUILD_MEMBERS_CHUNK
/// answers. GUILD_CREATE alone lists only part of the members of a large guild (docs/platform-parity.md).
/// </summary>
/// <remarks>
/// NetCord handles gateway messages concurrently, so chunks can arrive in any order and in parallel.
/// </remarks>
internal sealed class DiscordMemberDownloader(IDiscordGatewayCommands commands, TimeProvider timeProvider) : IGuildUserChunkGatewayHandler
{
    /// <summary>
    /// How long a download may take before it fails. Discord sends 1,000 members per chunk, so this is generous.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, PendingDownload> _pending = new();

    /// <exception cref="TimeoutException">Not every chunk arrived within <see cref="Timeout"/>.</exception>
    public async Task<IReadOnlyList<GuildUser>> DownloadAsync(ulong guildId, CancellationToken cancellationToken)
    {
        // The nonce ties the chunks to this request; Discord allows at most 32 characters.
        var nonce = Guid.NewGuid().ToString("N");
        var pending = new PendingDownload();
        _pending[nonce] = pending;
        try
        {
            await commands.RequestGuildUsersAsync(
                new GuildUsersRequestProperties(guildId) { Query = string.Empty, Limit = 0, Nonce = nonce },
                cancellationToken);
            return await pending.Completion.WaitAsync(Timeout, timeProvider, cancellationToken);
        }
        finally
        {
            _pending.TryRemove(nonce, out _);
        }
    }

    public ValueTask HandleAsync(GuildUserChunkEventArgs arg)
    {
        if (arg.Nonce is { } nonce && _pending.TryGetValue(nonce, out var pending))
        {
            pending.Add(arg);
        }

        return default;
    }

    private sealed class PendingDownload
    {
        private readonly Lock _lock = new();
        private readonly HashSet<int> _chunks = [];
        private readonly List<GuildUser> _users = [];
        private readonly TaskCompletionSource<IReadOnlyList<GuildUser>> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IReadOnlyList<GuildUser>> Completion => _completion.Task;

        public void Add(GuildUserChunkEventArgs chunk)
        {
            lock (_lock)
            {
                if (!_chunks.Add(chunk.ChunkIndex))
                {
                    return;
                }

                _users.AddRange(chunk.Users);
                if (_chunks.Count == chunk.ChunkCount)
                {
                    _completion.TrySetResult([.. _users]);
                }
            }
        }
    }
}
