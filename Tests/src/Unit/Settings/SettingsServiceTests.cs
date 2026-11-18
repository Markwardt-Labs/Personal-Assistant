namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly TempFolder folder = new();

    public void Dispose()
        => folder.Dispose();

    private SettingsService Create()
        => new(new JsonFile<StoredSettings>(folder.Combine("settings.json")));

    [Fact]
    public async Task StartsWithNoLocation()
    {
        SettingsService settings = Create();

        await settings.Ready(CancellationToken.None);

        Assert.Null(settings.Location);
    }

    [Fact]
    public async Task KeepsWhatTheUserSetAcrossRestarts()
    {
        await Create().Update(new() { Location = "  Austin, Texas  " }, CancellationToken.None);

        SettingsService restarted = Create();
        await restarted.Ready(CancellationToken.None);

        Assert.Equal("Austin, Texas", restarted.Location);
    }

    [Fact]
    public async Task ClearsTheLocationWhenItIsBlank()
    {
        SettingsService settings = Create();
        await settings.Update(new() { Location = "Austin" }, CancellationToken.None);

        await settings.Update(new() { Location = "" }, CancellationToken.None);

        Assert.Null(settings.Location);
    }

    [Fact]
    public async Task ReadsAFileThatAnEarlierVersionWroteAndDropsWhatItNoLongerUses()
    {
        await File.WriteAllTextAsync(folder.Combine("settings.json"), """{ "provider": "Codex", "location": "Austin", "googleClientId": "id", "googleClientSecret": "secret" }""");

        SettingsService settings = Create();
        await settings.Ready(CancellationToken.None);

        Assert.Equal("Austin", settings.Location);
        Assert.DoesNotContain("secret", await File.ReadAllTextAsync(folder.Combine("settings.json")));
    }
}
