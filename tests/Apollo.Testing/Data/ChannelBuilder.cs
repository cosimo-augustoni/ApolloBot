using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Testing.Data;

/// <summary>
/// Builds a <see cref="Channel"/> row with its snapshot columns. <see cref="SeedAsync"/> creates the internal ID (and
/// the guild's, if needed) through <see cref="IPlatformIdResolver"/> and stores the columns. Defaults to the text
/// channel <c>TestPlatform.Channel("general")</c> in <see cref="TestPlatform.Guild"/>.
/// </summary>
public sealed class ChannelBuilder : TestDataBuilder<ChannelBuilder, Channel>
{
    private PlatformRef _guild = TestPlatform.Guild;
    private PlatformRef _channel = TestPlatform.Channel("general");

    public ChannelBuilder For(PlatformRef channel, PlatformRef? guild = null)
    {
        _channel = channel;
        _guild = guild ?? _guild;
        return this;
    }

    public ChannelBuilder Named(string name) => With(c =>
    {
        c.Name = name;
        return c;
    });

    public ChannelBuilder OfType(PlatformChannelType type) => With(c =>
    {
        c.Type = type;
        return c;
    });

    /// <summary>
    /// A thread of <paramref name="parentId"/> (an internal ID).
    /// </summary>
    public ChannelBuilder AsThreadOf(Guid parentId) => With(c =>
    {
        c.Type = PlatformChannelType.Thread;
        c.ThreadParentId = parentId;
        return c;
    });

    public ChannelBuilder DeletedAt(DateTimeOffset deletedAt) => With(c =>
    {
        c.DeletedAt = deletedAt;
        return c;
    });

    /// <remarks>The category and thread parent are internal IDs; seed those channels first.</remarks>
    public async Task<Channel> SeedAsync(ApolloDbContext db, IPlatformIdResolver ids, CancellationToken cancellationToken)
    {
        var id = await ids.ResolveChannelAsync(_guild, _channel, cancellationToken);
        var row = await db.Channels.SingleAsync(c => c.Id == id, cancellationToken);
        var values = Build();
        row.Name = values.Name;
        row.Type = values.Type;
        row.CategoryId = values.CategoryId;
        row.ThreadParentId = values.ThreadParentId;
        row.DeletedAt = values.DeletedAt;
        await db.SaveChangesAsync(cancellationToken);
        return row;
    }

    protected override Channel CreateDefault() => new() { Name = "general", Type = PlatformChannelType.Text };
}
