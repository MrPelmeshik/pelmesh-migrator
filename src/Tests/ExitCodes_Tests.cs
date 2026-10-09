using Migrator;

namespace Tests;

public class ExitCodes_Tests
{
    [Test]
    public void Success_ReturnsZero()
    {
        var summary = new ApplySummary { Applied = 2, Errors = 0 };
        Assert.That(ExitCodes.Resolve(summary, OnErrorMode.Fail, failOnScriptErrors: false), Is.EqualTo(0));
    }

    [Test]
    public void Continue_WithErrors_WithoutFailOnScriptErrors_ReturnsZero()
    {
        var summary = new ApplySummary { Errors = 2 };
        Assert.That(ExitCodes.Resolve(summary, OnErrorMode.Continue, failOnScriptErrors: false), Is.EqualTo(0));
    }

    [Test]
    public void Continue_WithErrors_AndFailOnScriptErrors_ReturnsOne()
    {
        var summary = new ApplySummary { Errors = 1 };
        Assert.That(ExitCodes.Resolve(summary, OnErrorMode.Continue, failOnScriptErrors: true), Is.EqualTo(1));
    }

    [Test]
    public void Fail_WithErrors_ReturnsOne()
    {
        var summary = new ApplySummary { Errors = 1 };
        Assert.That(ExitCodes.Resolve(summary, OnErrorMode.Fail, failOnScriptErrors: false), Is.EqualTo(1));
    }
}
