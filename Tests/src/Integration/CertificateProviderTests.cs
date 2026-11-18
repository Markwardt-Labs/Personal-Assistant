namespace Markwardt.PersonalAssistant.Tests.Integration;

public sealed class CertificateProviderTests : IDisposable
{
    private readonly TempFolder folder = new();

    public void Dispose()
        => folder.Dispose();

    [Fact]
    public async Task MakesASelfSignedCertificateOnceAndReusesIt()
    {
        CertificateProvider provider = new();
        ServerConfig config = new() { Key = "key", DataFolder = folder.Path };

        using X509Certificate2 first = await provider.Load(config, CancellationToken.None);
        using X509Certificate2 second = await provider.Load(config, CancellationToken.None);

        Assert.True(first.HasPrivateKey);
        Assert.Equal(first.Thumbprint, second.Thumbprint);
        Assert.True(File.Exists(folder.Combine("certificate.pfx")));
    }

    [Fact]
    public async Task PresentsTheConfiguredCertificateInstead()
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new("CN=Mine", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 mine = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        await File.WriteAllBytesAsync(folder.Combine("mine.pfx"), mine.Export(X509ContentType.Pfx, "pw"));

        using X509Certificate2 loaded = await new CertificateProvider().Load(new() { Key = "key", DataFolder = folder.Path, CertificateFile = folder.Combine("mine.pfx"), CertificatePassword = "pw" }, CancellationToken.None);

        Assert.Equal(mine.Thumbprint, loaded.Thumbprint);
        Assert.False(File.Exists(folder.Combine("certificate.pfx")));
    }
}
