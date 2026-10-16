using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.DataProtection;

/// <summary>
/// <c>data_protection_keys</c>: the web host's ASP.NET Core data protection key ring (#29), so session cookies stay
/// valid across restarts and redeploys. Written only by the data protection system.
/// </summary>
internal static class DataProtectionModel
{
    public static void Configure(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<DataProtectionKey>(entity =>
        {
            entity.ToTable("data_protection_keys");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.FriendlyName).HasColumnName("friendly_name");
            entity.Property(x => x.Xml).HasColumnName("xml");
        });
}
