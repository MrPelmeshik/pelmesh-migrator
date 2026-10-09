using Migrator;

namespace Tests;

public class Extends_DescribeMismatch_Tests
{
    private const string EmptySql = "-- empty sql";

    [Test]
    public void HashMismatch_ContainsExpectedAndActual()
    {
        var scripts = new List<MigrationScript>
        {
            new(1, "1.a.sql", "a", EmptySql)
        };
        var histories = new List<MigrationHistory>
        {
            new(1, Extends.ComputeHash(EmptySql + "changed"), "1.a.sql")
        };

        var message = Extends.DescribeMigrationMismatch(scripts, histories, verifyHash: true);

        Assert.That(message, Is.Not.Null);
        Assert.That(message, Does.Contain("order=1"));
        Assert.That(message, Does.Contain("expected="));
        Assert.That(message, Does.Contain("actual="));
    }

    [Test]
    public void HashMismatch_IgnoredWhenVerifyOff()
    {
        var scripts = new List<MigrationScript>
        {
            new(1, "1.a.sql", "a", EmptySql)
        };
        var histories = new List<MigrationHistory>
        {
            new(1, Extends.ComputeHash(EmptySql + "changed"), "1.a.sql")
        };

        var message = Extends.DescribeMigrationMismatch(scripts, histories, verifyHash: false);
        Assert.That(message, Is.Null);
    }
}
