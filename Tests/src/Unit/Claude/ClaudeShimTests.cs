namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class ClaudeShimTests
{
    private readonly Dictionary<string, string?> variables = new() { ["PERSONAL_ASSISTANT_CLAUDE_SHIM"] = "1" };

    private readonly List<ProcessStartInfo> started = [];

    private int exitCode;

    private ClaudeShim Create(string? processPath = "/app/server")
        => new(processPath, "/base", name => variables.GetValueOrDefault(name), (info, _) =>
        {
            started.Add(info);
            return Task.FromResult(exitCode);
        });

    private string[] Session(string instructions, params string[] more)
        => ["-p", "--input-format", "stream-json", "--strict-mcp-config", "--append-system-prompt", instructions, .. more];

    [Fact]
    public void IsTheLauncherOnlyForTheLibrarysArgumentsWithTheMarkerVariable()
    {
        ClaudeShim shim = Create();

        Assert.True(shim.IsShim(["-p", "--output-format", "json"]));
        Assert.False(shim.IsShim(["--data-folder", "x"]));
        Assert.False(shim.IsShim([]));

        variables.Remove("PERSONAL_ASSISTANT_CLAUDE_SHIM");

        Assert.False(shim.IsShim(["-p"]));
    }

    [Fact]
    public void LeavesASessionWithoutTheMarkerAlone()
    {
        ClaudeShim shim = Create();
        string[] arguments = Session("Be helpful.", "--allowedTools", "mcp__liveai");

        Assert.Equal(arguments, shim.Rewrite(arguments));
    }

    [Fact]
    public void LeavesCallsWithoutInstructionsAlone()
    {
        ClaudeShim shim = Create();
        string[] arguments = ["-p", "--strict-mcp-config", "--model", "haiku"];

        Assert.Equal(arguments, shim.Rewrite(arguments));
    }

    [Fact]
    public void LoadsAndAllowsOnlyTheTwoConnectorsForASessionWithTheMarker()
    {
        ClaudeShim shim = Create();

        IReadOnlyList<string> changed = shim.Rewrite(Session($"Be helpful.\n\n{shim.Marker}", "--allowedTools", "mcp__liveai,WebSearch", "--resume", "abc"));

        Assert.DoesNotContain("--strict-mcp-config", changed);
        int allowed = changed.ToList().IndexOf("--allowedTools");
        Assert.Equal("mcp__liveai,WebSearch,mcp__claude_ai_Gmail,mcp__claude_ai_Google_Calendar", changed[allowed + 1]);
        Assert.Equal(["--resume", "abc"], changed.Skip(allowed + 2));
        Assert.Contains("stream-json", changed);
    }

    [Fact]
    public void AddsTheAllowedToolsWhenTheLibraryGaveNone()
    {
        ClaudeShim shim = Create();

        IReadOnlyList<string> changed = shim.Rewrite(Session(shim.Marker));

        Assert.Equal(["--allowedTools", "mcp__claude_ai_Gmail,mcp__claude_ai_Google_Calendar"], changed.TakeLast(2));
        Assert.DoesNotContain("--strict-mcp-config", changed);
    }

    [Fact]
    public async Task StartsTheRealClaudeWithTheRewrittenArgumentsAndGivesItsExitCode()
    {
        ClaudeShim shim = Create();
        exitCode = 7;

        int code = await shim.Run(Session(shim.Marker), CancellationToken.None);

        Assert.Equal(7, code);
        ProcessStartInfo info = Assert.Single(started);
        Assert.Equal("claude", info.FileName);
        Assert.False(info.UseShellExecute);
        Assert.False(info.RedirectStandardInput);
        Assert.False(info.RedirectStandardOutput);
        Assert.DoesNotContain("--strict-mcp-config", info.ArgumentList);
    }

    [Fact]
    public void IsItsOwnExecutableAsTheCommand()
        => Assert.Equal("/app/server", Create("/app/server").Command);

    [Fact]
    public void FindsTheExecutableBesideTheApplicationWhenRunByTheDotnetCommand()
    {
        string command = Create("/usr/share/dotnet/dotnet").Command;

        Assert.Equal(Path.Combine("/base", OperatingSystem.IsWindows() ? "Markwardt.PersonalAssistant.Server.exe" : "Markwardt.PersonalAssistant.Server"), command);
        Assert.Equal(command, Create(null).Command);
    }
}
