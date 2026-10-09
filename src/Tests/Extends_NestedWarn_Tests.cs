using Migrator;

namespace Tests;

public class Extends_NestedWarn_Tests
{
    private const string EmptySql = "-- empty sql";

    [Test]
    public void Mismatch_ProducesWarning()
    {
        var scripts = new[]
        {
            new MigrationScript(1, "001.other.meta.sql", "other.meta", EmptySql)
        };
        var writer = new StringWriter();

        var warnings = Extends.WarnNestedDatabaseMismatch("demo_db", scripts, writer);

        Assert.That(warnings.Count, Is.EqualTo(1));
        Assert.That(warnings[0], Does.Contain("other"));
        Assert.That(warnings[0], Does.Contain("demo_db"));
        Assert.That(writer.ToString(), Does.Contain("WARNING"));
    }

    [Test]
    public void MatchingSegment_NoWarning()
    {
        var scripts = new[]
        {
            new MigrationScript(1, "001.demo_db.meta.sql", "demo_db.meta", EmptySql)
        };

        var warnings = Extends.WarnNestedDatabaseMismatch("demo_db", scripts, TextWriter.Null);
        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void TwoSegmentName_NoWarning()
    {
        var scripts = new[]
        {
            new MigrationScript(1, "0001.init.sql", "init", EmptySql)
        };

        var warnings = Extends.WarnNestedDatabaseMismatch("demo_db", scripts, TextWriter.Null);
        Assert.That(warnings, Is.Empty);
    }
}
