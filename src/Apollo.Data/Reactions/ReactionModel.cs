using Apollo.Data.Identity;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Reactions;

/// <summary>
/// The <c>reaction_events</c> hypertable (#17).
/// </summary>
internal static class ReactionModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReactionEvent>(entity =>
        {
            entity.ToTable("reaction_events", table =>
                table.HasCheckConstraint("CK_reaction_events_unicode_emoji", "emoji_custom_id IS NOT NULL OR emoji_name <> ''"));
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.ChannelId).HasColumnName("channel_id");
            entity.Property(x => x.MessageExtId).HasColumnName("message_ext_id");
            entity.Property(x => x.MessageAuthorId).HasColumnName("message_author_id");
            entity.Property(x => x.EmojiName).HasColumnName("emoji_name");
            entity.Property(x => x.EmojiCustomId).HasColumnName("emoji_custom_id");
            entity.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>();

            // Hypertable rule: the primary key must contain the partition column (ADR 0001).
            entity.HasKey(x => new { x.Id, x.OccurredAt });
            entity.IsHypertable(x => x.OccurredAt);

            // Finds the earlier add row of a reaction whose event has no message author.
            entity.HasIndex(x => new { x.GuildId, x.MessageExtId });

            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Channel>().WithMany().HasForeignKey(x => x.ChannelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.MessageAuthorId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
