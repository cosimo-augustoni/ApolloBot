using Apollo.Data.Identity;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Messages;

/// <summary>
/// The <c>message_events</c> and <c>message_mentions</c> hypertables (#15).
/// </summary>
internal static class MessageModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MessageEvent>(entity =>
        {
            entity.ToTable("message_events");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.ChannelId).HasColumnName("channel_id");
            entity.Property(x => x.ThreadParentId).HasColumnName("thread_parent_id");
            entity.Property(x => x.MessageExtId).HasColumnName("message_ext_id");
            entity.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>();
            entity.Property(x => x.Length).HasColumnName("length");
            entity.Property(x => x.WordCount).HasColumnName("word_count");
            entity.Property(x => x.AttachmentCount).HasColumnName("attachment_count");
            entity.Property(x => x.EmbedCount).HasColumnName("embed_count");
            entity.Property(x => x.EmojiCount).HasColumnName("emoji_count");
            entity.Property(x => x.UserMentionCount).HasColumnName("user_mention_count");
            entity.Property(x => x.RoleMentionCount).HasColumnName("role_mention_count");
            entity.Property(x => x.MentionsEveryone).HasColumnName("mentions_everyone");
            entity.Property(x => x.ContentParts).HasColumnName("content_parts");
            entity.Property(x => x.ReplyToUserId).HasColumnName("reply_to_user_id");
            entity.Property(x => x.IsBot).HasColumnName("is_bot");

            // Hypertable rule: the primary key must contain the partition column (ADR 0001).
            entity.HasKey(x => new { x.Id, x.OccurredAt });
            entity.IsHypertable(x => x.OccurredAt);

            // Finds the create row of a message: author-less edits and deletes, replies without the replied-to author.
            entity.HasIndex(x => new { x.GuildId, x.MessageExtId });

            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Channel>().WithMany().HasForeignKey(x => x.ChannelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Channel>().WithMany().HasForeignKey(x => x.ThreadParentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.ReplyToUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MessageMention>(entity =>
        {
            entity.ToTable("message_mentions");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.ChannelId).HasColumnName("channel_id");
            entity.Property(x => x.MessageExtId).HasColumnName("message_ext_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.MentionedUserId).HasColumnName("mentioned_user_id");

            entity.HasKey(x => new { x.Id, x.OccurredAt });
            entity.IsHypertable(x => x.OccurredAt);

            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Channel>().WithMany().HasForeignKey(x => x.ChannelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.MentionedUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
