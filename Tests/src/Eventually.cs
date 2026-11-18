namespace Markwardt.PersonalAssistant.Tests;

internal sealed class Eventually
{
    public async Task Be(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));

        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
