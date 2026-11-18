namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class AppInfoTests
{
    [Fact]
    public void ReportsTheProductNameAndTheMajorMinorPatchOfTheAssembly()
    {
        IAppInfo info = new IAppInfo.Factory().Create(typeof(AppInfoTests).Assembly);

        Assert.Equal("Personal Assistant", info.Name);
        Assert.Equal(typeof(AppInfoTests).Assembly.GetName().Version!.ToString(3), info.Version);
    }
}
