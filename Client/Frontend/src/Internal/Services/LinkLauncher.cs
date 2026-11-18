namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// Opens a web address in the browser of the phone.
/// </summary>
internal interface ILinkLauncher
{
    /// <summary>
    /// Opens the address.
    /// </summary>
    /// <param name="address">The address; only http and https are opened.</param>
    /// <returns><see langword="true" /> if the browser was started.</returns>
    Task<bool> Open(string address);
}

/// <inheritdoc />
internal sealed class LinkLauncher(Func<TopLevel?> topLevel) : ILinkLauncher
{
    /// <inheritdoc />
    public async Task<bool> Open(string address)
        => Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https" && topLevel() is { } top && await top.Launcher.LaunchUriAsync(uri);
}
