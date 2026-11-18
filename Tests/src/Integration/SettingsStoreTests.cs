namespace Markwardt.PersonalAssistant.Tests.Integration;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly TempFolder folder = new();

    public void Dispose()
        => folder.Dispose();

    [Fact]
    public async Task KeepsTheSettingsAcrossRestarts()
    {
        ConnectionSettings settings = new() { Host = "pc", Port = 52680, AccessKey = "key" };
        await new SettingsStore(folder.Combine("nested/settings.json")).Save(settings, CancellationToken.None);

        Assert.Equal(settings, await new SettingsStore(folder.Combine("nested/settings.json")).Load(CancellationToken.None));
    }

    [Fact]
    public async Task HasNothingBeforeTheFirstSave()
    {
        Assert.Null(await new SettingsStore(folder.Combine("settings.json")).Load(CancellationToken.None));
    }

    [Fact]
    public async Task IgnoresAFileThatCannotBeRead()
    {
        await File.WriteAllTextAsync(folder.Combine("settings.json"), "garbage");

        Assert.Null(await new SettingsStore(folder.Combine("settings.json")).Load(CancellationToken.None));
    }

    [Fact]
    public async Task KeepsTheAccessKeyReadableByTheUserOnly()
    {
        await new SettingsStore(folder.Combine("settings.json")).Save(new() { Host = "pc", Port = 1, AccessKey = "key" }, CancellationToken.None);

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(folder.Combine("settings.json")));
        }
    }
}
