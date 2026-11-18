namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// How to reach the server, read from its config file in the app data folder: everything else about the server is set from the client.
/// </summary>
internal sealed record ServerConfig
{
    /// <summary>
    /// Gets the port the server listens on, for every client.
    /// </summary>
    public int Port { get; init; } = 52680;

    /// <summary>
    /// Gets the access key a client must send first.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the PFX file to present as the certificate, or <see langword="null" /> for a self-signed one the server makes in its app data folder.
    /// </summary>
    public string? CertificateFile { get; init; }

    /// <summary>
    /// Gets the password of <see cref="CertificateFile" />, if it has one.
    /// </summary>
    public string? CertificatePassword { get; init; }

    /// <summary>
    /// Gets the app data folder that holds the config and everything the server keeps, which the loader sets and the file does not contain.
    /// </summary>
    [JsonIgnore]
    public string? DataFolder { get; init; }
}
