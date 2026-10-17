using Apollo.Data.Identity;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Sessions;

/// <summary>
/// <c>user_sign_outs</c>: the web host's logout revocation (#30). A reference table, not an event table.
/// </summary>
internal static class SessionModel
{
    public static void Configure(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<UserSignOut>(entity =>
        {
            entity.ToTable("user_sign_outs");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasColumnName("user_id").ValueGeneratedNever();
            entity.Property(x => x.SignedOutAt).HasColumnName("signed_out_at");
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
}
