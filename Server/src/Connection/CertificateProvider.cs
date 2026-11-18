namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Supplies the certificate the server presents over TLS.
/// </summary>
internal interface ICertificateProvider
{
    /// <summary>
    /// Loads the configured certificate, or the self-signed one in the data folder, making it on the first start.
    /// </summary>
    /// <param name="config">The server settings.</param>
    /// <param name="cancellation">Cancels the work.</param>
    /// <returns>The certificate with its private key, which the caller disposes.</returns>
    Task<X509Certificate2> Load(ServerConfig config, CancellationToken cancellation = default);
}

/// <inheritdoc />
internal sealed class CertificateProvider : ICertificateProvider
{
    /// <inheritdoc />
    public async Task<X509Certificate2> Load(ServerConfig config, CancellationToken cancellation = default)
    {
        if (config.CertificateFile is { } file)
        {
            return X509CertificateLoader.LoadPkcs12FromFile(file, config.CertificatePassword);
        }

        string path = Path.Combine(config.DataFolder!, "certificate.pfx");

        if (!File.Exists(path))
        {
            using RSA key = RSA.Create(2048);
            CertificateRequest request = new("CN=Personal Assistant", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
            SubjectAlternativeNameBuilder names = new();
            names.AddDnsName("localhost");
            names.AddDnsName(Environment.MachineName);
            request.CertificateExtensions.Add(names.Build());
            using X509Certificate2 created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
            await File.WriteAllBytesAsync(path, created.Export(X509ContentType.Pfx), cancellation);

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }

        return X509CertificateLoader.LoadPkcs12FromFile(path, null);
    }
}
