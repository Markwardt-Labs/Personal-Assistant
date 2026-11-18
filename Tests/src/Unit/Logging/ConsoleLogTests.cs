namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class ConsoleLogTests
{
    private readonly StringWriter output = new();

    private ConsoleLog Create()
        => new(output, TimeProvider.System);

    [Fact]
    public void WritesInformationWithATimestamp()
    {
        Create().Info("Listening");

        Assert.Matches(@"^\d\d:\d\d:\d\d Listening\r?\n$", output.ToString());
    }

    [Fact]
    public void WritesAFailureWithItsException()
    {
        Create().Error("Could not save", new IOException("disk full"));

        Assert.Contains("Error: Could not save (IOException: disk full)", output.ToString());
    }

    [Fact]
    public void WritesAFailureWithoutAnException()
    {
        Create().Error("Could not save");

        Assert.Contains("Error: Could not save", output.ToString());
        Assert.DoesNotContain("(", output.ToString());
    }
}
