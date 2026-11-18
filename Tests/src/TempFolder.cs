namespace Markwardt.PersonalAssistant.Tests;

public sealed class TempFolder : IDisposable
{
    public TempFolder()
        => Directory.CreateDirectory(Path);

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "personalassistant-" + Guid.NewGuid().ToString("N"));

    public string Combine(string name)
        => System.IO.Path.Combine(Path, name);

    public void Dispose()
        => Directory.Delete(Path, true);
}
