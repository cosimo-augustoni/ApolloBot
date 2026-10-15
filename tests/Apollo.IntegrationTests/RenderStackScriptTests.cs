using System.Diagnostics;

namespace Apollo.IntegrationTests;

/// <summary>
/// Runs <c>deploy/Render-Stack.ps1</c> with pwsh against hand-written compose and env files (#8).
/// </summary>
public sealed class RenderStackScriptTests : IDisposable
{
    private const string Secret = "s3cr3t-value";

    private readonly string _outputPath = Path.Combine(Path.GetTempPath(), "apollo-render-" + Guid.NewGuid().ToString("N"));

    public RenderStackScriptTests() => Directory.CreateDirectory(_outputPath);

    private string StackFile => Path.Combine(_outputPath, "apollo.stack.yaml");

    [Fact]
    public async Task Render_WhenAllValuesPresent_ShouldReplaceEveryPlaceholder()
    {
        WriteCompose("""
            services:
              bot:
                image: "${BOT_IMAGE}"
                environment:
                  ConnectionStrings__apollo: "Host=db;Password=${DB_PASSWORD};Database=apollo"
            """);
        WriteEnv(".env", "# Container image name for bot", "BOT_IMAGE=registry.example.test/apollo/bot:1", "", $"DB_PASSWORD={Secret}");

        var result = await RunAsync();

        result.ExitCode.ShouldBe(0, result.Output);
        var stack = await File.ReadAllTextAsync(StackFile, TestContext.Current.CancellationToken);
        stack.ShouldNotContain("${");
        stack.ShouldContain("image: \"registry.example.test/apollo/bot:1\"");
        stack.ShouldContain($"Password={Secret};");
    }

    [Fact]
    public async Task Render_WhenValueMissing_ShouldUseDefault()
    {
        WriteCompose("""port: "${WEB_PORT:-8080}" """);
        WriteEnv(".env");

        var result = await RunAsync();

        result.ExitCode.ShouldBe(0, result.Output);
        (await File.ReadAllTextAsync(StackFile, TestContext.Current.CancellationToken)).ShouldContain("port: \"8080\"");
    }

    [Fact]
    public async Task Render_WhenValuePresent_ShouldPreferItOverDefault()
    {
        WriteCompose("""port: "${WEB_PORT:-8080}" """);
        WriteEnv(".env", "WEB_PORT=80");

        var result = await RunAsync();

        result.ExitCode.ShouldBe(0, result.Output);
        (await File.ReadAllTextAsync(StackFile, TestContext.Current.CancellationToken)).ShouldContain("port: \"80\"");
    }

    [Theory]
    [InlineData("")]
    [InlineData("BOT_IMAGE=")]
    public async Task Render_WhenValueMissingOrEmpty_ShouldFailAndNameMissingVariablesWithoutWritingStack(string botImageLine)
    {
        WriteCompose("""
            image: "${BOT_IMAGE}"
            password: "${DB_PASSWORD}"
            other: "${OTHER_IMAGE}"
            """);
        WriteEnv(".env", botImageLine, $"DB_PASSWORD={Secret}");

        var result = await RunAsync();

        result.ExitCode.ShouldNotBe(0);
        File.Exists(StackFile).ShouldBeFalse();
        result.Output.ShouldContain("BOT_IMAGE");
        result.Output.ShouldContain("OTHER_IMAGE");
        result.Output.ShouldNotContain("DB_PASSWORD");
        result.Output.ShouldNotContain(Secret);
    }

    [Fact]
    public async Task Render_WhenSuccessful_ShouldNotPrintValues()
    {
        WriteCompose("""password: "${DB_PASSWORD}" """);
        WriteEnv(".env", $"DB_PASSWORD={Secret}");

        var result = await RunAsync();

        result.ExitCode.ShouldBe(0, result.Output);
        result.Output.ShouldNotContain(Secret);
    }

    [Fact]
    public async Task Render_ValueWithSpecialCharacters_ShouldStayLiteralForYamlAndStackDeploy()
    {
        // docker stack deploy interpolates $ again, and every placeholder sits in a double-quoted YAML scalar.
        WriteCompose("""password: "${DB_PASSWORD}" """);
        WriteEnv(".env", """DB_PASSWORD=a$b"c\d""");

        var result = await RunAsync();

        result.ExitCode.ShouldBe(0, result.Output);
        (await File.ReadAllTextAsync(StackFile, TestContext.Current.CancellationToken))
            .ShouldContain("""password: "a$$b\"c\\d" """.TrimEnd());
    }

    [Fact]
    public async Task Render_EscapedPlaceholder_ShouldBeLeftAlone()
    {
        WriteCompose("""literal: "$${NOT_A_VARIABLE}" """);
        WriteEnv(".env");

        var result = await RunAsync();

        result.ExitCode.ShouldBe(0, result.Output);
        (await File.ReadAllTextAsync(StackFile, TestContext.Current.CancellationToken)).ShouldContain("$${NOT_A_VARIABLE}");
    }

    [Fact]
    public async Task Render_WhenOnlyEnvironmentSpecificEnvFileExists_ShouldUseIt()
    {
        WriteCompose("""image: "${BOT_IMAGE}" """);
        WriteEnv(".env.Production", "BOT_IMAGE=bot:1");

        var result = await RunAsync();

        result.ExitCode.ShouldBe(0, result.Output);
        (await File.ReadAllTextAsync(StackFile, TestContext.Current.CancellationToken)).ShouldContain("image: \"bot:1\"");
    }

    [Fact]
    public async Task Render_WhenTemplateAndEnvironmentEnvFilesExist_ShouldUseEnvironmentFile()
    {
        // aspire publish writes .env with empty values; prepare-docker-compose writes .env.Production with the real ones.
        WriteCompose("""image: "${BOT_IMAGE}" """);
        WriteEnv(".env", "BOT_IMAGE=");
        WriteEnv(".env.Production", "BOT_IMAGE=bot:1");

        var result = await RunAsync();

        result.ExitCode.ShouldBe(0, result.Output);
        (await File.ReadAllTextAsync(StackFile, TestContext.Current.CancellationToken)).ShouldContain("image: \"bot:1\"");
    }

    [Fact]
    public async Task Render_WhenEnvironmentGiven_ShouldUseThatEnvFile()
    {
        WriteCompose("""image: "${BOT_IMAGE}" """);
        WriteEnv(".env.Production", "BOT_IMAGE=bot:1");
        WriteEnv(".env.Staging", "BOT_IMAGE=bot:2");

        var result = await RunAsync("-Environment", "Staging");

        result.ExitCode.ShouldBe(0, result.Output);
        (await File.ReadAllTextAsync(StackFile, TestContext.Current.CancellationToken)).ShouldContain("image: \"bot:2\"");
    }

    [Fact]
    public async Task Render_WhenNoEnvFile_ShouldFail()
    {
        WriteCompose("""image: "${BOT_IMAGE}" """);

        var result = await RunAsync();

        result.ExitCode.ShouldNotBe(0);
        File.Exists(StackFile).ShouldBeFalse();
        result.Output.ShouldContain(".env.Production");
    }

    [Fact]
    public async Task Render_WhenComposeFileMissing_ShouldFail()
    {
        WriteEnv(".env");

        var result = await RunAsync();

        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("docker-compose.yaml");
    }

    public void Dispose() => Directory.Delete(_outputPath, recursive: true);

    private void WriteCompose(string content) =>
        File.WriteAllText(Path.Combine(_outputPath, "docker-compose.yaml"), content);

    private void WriteEnv(string fileName, params string[] lines) =>
        File.WriteAllLines(Path.Combine(_outputPath, fileName), lines);

    private async Task<(int ExitCode, string Output)> RunAsync(params string[] extraArguments)
    {
        var startInfo = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", ScriptPath, "-OutputPath", _outputPath }.Concat(extraArguments))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start pwsh.");
        var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        return (process.ExitCode, await stdout + await stderr);
    }

    private static string ScriptPath
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApolloBot.slnx")))
            {
                directory = directory.Parent;
            }

            return Path.Combine(
                directory?.FullName ?? throw new InvalidOperationException("Repository root not found."),
                "deploy",
                "Render-Stack.ps1");
        }
    }
}
