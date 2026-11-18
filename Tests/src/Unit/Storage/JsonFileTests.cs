namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class JsonFileTests : IDisposable
{
    private readonly TempFolder folder = new();

    public void Dispose()
        => folder.Dispose();

    [Fact]
    public async Task KeepsAValueAcrossRestartsWithEnumsAsNames()
    {
        MailDigest digest = new() { Generated = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero), Topics = [new() { Id = "t1", Title = "Tax", Summary = "Due.", Importance = MailImportance.Critical, Messages = [] }] };
        await new JsonFile<MailDigest>(folder.Combine("digest.json")).Save(digest, CancellationToken.None);

        MailDigest? loaded = await new JsonFile<MailDigest>(folder.Combine("digest.json")).Load(CancellationToken.None);

        Assert.Equal(MailImportance.Critical, loaded!.Topics.Single().Importance);
        Assert.Contains("\"critical\"", (await File.ReadAllTextAsync(folder.Combine("digest.json"))).ToLowerInvariant());
        Assert.False(File.Exists(folder.Combine("digest.json.tmp")));
    }

    [Fact]
    public async Task HasNothingBeforeTheFirstSaveOrWhenTheFileIsBroken()
    {
        JsonFile<MailDigest> file = new(folder.Combine("digest.json"));
        Assert.Null(await file.Load(CancellationToken.None));

        await File.WriteAllTextAsync(folder.Combine("digest.json"), "garbage");

        Assert.Null(await file.Load(CancellationToken.None));
    }
}
