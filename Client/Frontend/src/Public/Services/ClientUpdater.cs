namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// Installs a release of the app over the running one, which only the head that is installed on its own can do: the Android head hands the downloaded package to the system installer.
/// </summary>
public interface IClientUpdater
{
    /// <summary>
    /// Gets a value indicating whether this run of the app can be replaced; without it the client offers no update for itself and only updates its server.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Downloads the file of the release for this platform and installs it; the call returns once the system installer is showing.
    /// </summary>
    /// <param name="release">The release to install.</param>
    /// <param name="progress">Receives the fraction downloaded, from 0 to 1.</param>
    /// <param name="cancellation">Cancels the installation before anything is replaced.</param>
    /// <exception cref="IOException">The release has no file for this platform or the download or installation failed.</exception>
    /// <exception cref="InvalidOperationException">This run of the app cannot be replaced, or the user has to allow installing apps first.</exception>
    Task Install(ReleaseInfo release, IProgress<double> progress, CancellationToken cancellation = default);
}
