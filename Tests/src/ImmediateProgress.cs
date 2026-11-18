namespace Markwardt.PersonalAssistant.Tests;

internal sealed class ImmediateProgress(List<double> reported) : IProgress<double>
{
    public void Report(double value)
        => reported.Add(value);
}
