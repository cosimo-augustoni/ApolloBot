using Apollo.Data;
using Apollo.Data.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Apollo.IntegrationTests.Data;

public sealed class ApolloDataExtensionsTests
{
    [Fact]
    public void AddApolloDbContext_WhenTimeoutNotConfigured_ShouldDefaultToFiveMinutes()
    {
        using var host = BuildHost(timeout: null);

        host.Services.GetRequiredService<IOptions<DataOptions>>().Value.MigrationWaitTimeout
            .ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:00:01")]
    public void AddApolloDbContext_WhenTimeoutNotPositive_ShouldFailValidation(string timeout)
    {
        using var host = BuildHost(timeout);

        Should.Throw<OptionsValidationException>(() => host.Services.GetRequiredService<IOptions<DataOptions>>().Value)
            .Message.ShouldContain(nameof(DataOptions.MigrationWaitTimeout));
    }

    [Fact]
    public void AddApolloDbContext_ShouldRegisterContext()
    {
        using var host = BuildHost(timeout: null);
        using var scope = host.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<ApolloDbContext>().ShouldNotBeNull();
    }

    [Fact]
    public void AddApolloDbContext_ShouldRegisterScopedResolverWithOneSharedCache()
    {
        using var host = BuildHost(timeout: null);
        using var first = host.Services.CreateScope();
        using var second = host.Services.CreateScope();

        first.ServiceProvider.GetRequiredService<IPlatformIdResolver>().ShouldBeOfType<PlatformIdResolver>();
        first.ServiceProvider.GetRequiredService<PlatformIdCache>()
            .ShouldBeSameAs(second.ServiceProvider.GetRequiredService<PlatformIdCache>());
    }

    private static IHost BuildHost(string? timeout)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        var settings = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{ApolloDbContext.ConnectionName}"] = "Host=localhost;Database=apollo",
        };
        if (timeout is not null)
        {
            settings[$"{DataOptions.SectionName}:{nameof(DataOptions.MigrationWaitTimeout)}"] = timeout;
        }

        builder.Configuration.AddInMemoryCollection(settings);
        builder.AddApolloDbContext();

        return builder.Build();
    }
}
