namespace Markwardt.PersonalAssistant.Tests.Integration;

public sealed class ConfigLoaderTests : IDisposable
{
    private readonly TempFolder folder = new();

    public void Dispose()
        => folder.Dispose();

    [Fact]
    public async Task MakesAConfigWithAFreshKeyInTheAppFolderOnTheFirstStart()
    {
        ConfigLoader loader = new(folder.Combine("app"));

        ServerConfig config = await loader.Load([], CancellationToken.None);

        Assert.Equal(64, config.Key.Length);
        Assert.Equal(52680, config.Port);
        Assert.Equal(folder.Combine("app"), config.DataFolder);
        Assert.True(File.Exists(folder.Combine("app/config.json")));
        Assert.Equal(config.Key, (await loader.Load([], CancellationToken.None)).Key);
    }

    [Fact]
    public async Task KeepsOnlyHowToReachTheServerInTheFile()
    {
        await new ConfigLoader(folder.Path).Load([], CancellationToken.None);

        using JsonDocument file = JsonDocument.Parse(await File.ReadAllTextAsync(folder.Combine("config.json")));
        Assert.Equal(["key", "port"], file.RootElement.EnumerateObject().Select(property => property.Name).Order());
    }

    [Fact]
    public async Task ReadsThePortAndTheCertificateOfTheFile()
    {
        await File.WriteAllTextAsync(folder.Combine("config.json"), """{ "port": 4000, "key": "abc", "certificateFile": "mine.pfx", "certificatePassword": "pw" }""");

        ServerConfig config = await new ConfigLoader(folder.Path).Load([], CancellationToken.None);

        Assert.Equal((4000, "abc", "mine.pfx", "pw"), (config.Port, config.Key, config.CertificateFile, config.CertificatePassword));
    }

    [Fact]
    public async Task IgnoresSettingsThatBelongInTheClientAndAFolderNamedInTheFile()
    {
        await File.WriteAllTextAsync(folder.Combine("config.json"), """{ "key": "abc", "provider": "codex", "location": "Austin", "dataFolder": "/elsewhere" }""");

        ServerConfig config = await new ConfigLoader(folder.Path).Load([], CancellationToken.None);

        Assert.Equal(folder.Path, config.DataFolder);
    }

    [Fact]
    public async Task UsesTheFolderNamedOnTheCommandLineInsteadOfTheAppFolder()
    {
        ServerConfig config = await new ConfigLoader(folder.Combine("app")).Load(["--other", "--data-folder", folder.Combine("second")], CancellationToken.None);

        Assert.Equal(folder.Combine("second"), config.DataFolder);
        Assert.True(File.Exists(folder.Combine("second/config.json")));
        Assert.False(Directory.Exists(folder.Combine("app")));
    }

    [Fact]
    public async Task RefusesAFileThatIsNotAValidConfig()
    {
        await File.WriteAllTextAsync(folder.Combine("config.json"), "{ not json");

        await Assert.ThrowsAsync<InvalidDataException>(() => new ConfigLoader(folder.Path).Load([], CancellationToken.None));
    }

    [Fact]
    public async Task RefusesAnEmptyConfig()
    {
        await File.WriteAllTextAsync(folder.Combine("config.json"), "null");

        await Assert.ThrowsAsync<InvalidDataException>(() => new ConfigLoader(folder.Path).Load([], CancellationToken.None));
    }
}
