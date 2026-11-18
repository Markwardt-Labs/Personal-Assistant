namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class MailAddressesTests
{
    [Theory]
    [InlineData("Anna Berg <anna@example.com>", "Anna Berg")]
    [InlineData("\"Berg, Anna\" <anna@example.com>", "Berg, Anna")]
    [InlineData("anna@example.com", "anna@example.com")]
    [InlineData("<anna@example.com>", "anna@example.com")]
    [InlineData("not an address", "not an address")]
    [InlineData("", "")]
    public void ShowsTheNameOfTheSenderOrTheAddress(string field, string expected)
    {
        Assert.Equal(expected, field.DisplayName);
    }
}
