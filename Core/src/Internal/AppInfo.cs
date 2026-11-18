namespace Markwardt.PersonalAssistant;

/// <inheritdoc />
internal sealed class AppInfo(string version) : IAppInfo
{
    /// <inheritdoc />
    public string Name { get; } = "Personal Assistant";

    /// <inheritdoc />
    public string Version { get; } = version;
}
