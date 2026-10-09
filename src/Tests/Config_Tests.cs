using Migrator;

namespace Tests;

public class Config_Tests
{
    private const string ScriptsDir = "./test_config_scripts";

    [SetUp]
    public void SetUp()
    {
        if (!Directory.Exists(ScriptsDir))
            Directory.CreateDirectory(ScriptsDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(ScriptsDir))
            Directory.Delete(ScriptsDir, true);

        Environment.SetEnvironmentVariable("POSTGRES_HOST", null);
        Environment.SetEnvironmentVariable("POSTGRES_PORT", null);
        Environment.SetEnvironmentVariable("POSTGRES_USER", null);
        Environment.SetEnvironmentVariable("POSTGRES_PASSWORD", null);
        Environment.SetEnvironmentVariable("POSTGRES_DB", null);
    }

    [Test]
    public void Defaults_NestedFailAllVerifyHash()
    {
        var cfg = new Config([
            "--host", "h", "--port", "5432", "--user", "u", "--password", "p",
            "--scripts", ScriptsDir
        ]);

        Assert.That(cfg.Layout, Is.EqualTo(LayoutMode.Nested));
        Assert.That(cfg.OnError, Is.EqualTo(OnErrorMode.Fail));
        Assert.That(cfg.Transaction, Is.EqualTo(TransactionMode.All));
        Assert.That(cfg.VerifyHash, Is.True);
    }

    [Test]
    public void Continue_DefaultsTransactionToPerFile()
    {
        var cfg = new Config([
            "--host", "h", "--port", "5432", "--user", "u", "--password", "p",
            "--scripts", ScriptsDir,
            "--on-error", "continue"
        ]);

        Assert.That(cfg.Transaction, Is.EqualTo(TransactionMode.PerFile));
    }

    [Test]
    public void ContinuePlusAll_Throws()
    {
        Assert.Throws<Exception>(() => new Config([
            "--host", "h", "--port", "5432", "--user", "u", "--password", "p",
            "--scripts", ScriptsDir,
            "--on-error", "continue",
            "--transaction", "all"
        ]));
    }

    [Test]
    public void Flat_WithoutDatabase_Throws()
    {
        Assert.Throws<Exception>(() => new Config([
            "--host", "h", "--port", "5432", "--user", "u", "--password", "p",
            "--scripts", ScriptsDir,
            "--layout", "flat"
        ]));
    }

    [Test]
    public void EnvFallbacks_UsedWhenArgsMissing()
    {
        Environment.SetEnvironmentVariable("POSTGRES_HOST", "env-host");
        Environment.SetEnvironmentVariable("POSTGRES_PORT", "6543");
        Environment.SetEnvironmentVariable("POSTGRES_USER", "env-user");
        Environment.SetEnvironmentVariable("POSTGRES_PASSWORD", "env-pass");
        Environment.SetEnvironmentVariable("POSTGRES_DB", "env-db");

        var cfg = new Config([
            "--scripts", ScriptsDir,
            "--layout", "flat"
        ]);

        Assert.That(cfg.Host, Is.EqualTo("env-host"));
        Assert.That(cfg.Port, Is.EqualTo(6543));
        Assert.That(cfg.User, Is.EqualTo("env-user"));
        Assert.That(cfg.Password, Is.EqualTo("env-pass"));
        Assert.That(cfg.Database, Is.EqualTo("env-db"));
    }

    [Test]
    public void NoVerifyHash_DisablesVerification()
    {
        var cfg = new Config([
            "--host", "h", "--port", "5432", "--user", "u", "--password", "p",
            "--scripts", ScriptsDir,
            "--no-verify-hash"
        ]);

        Assert.That(cfg.VerifyHash, Is.False);
    }

    [Test]
    public void VerifyHashFalse_DisablesVerification()
    {
        var cfg = new Config([
            "--host", "h", "--port", "5432", "--user", "u", "--password", "p",
            "--scripts", ScriptsDir,
            "--verify-hash", "false"
        ]);

        Assert.That(cfg.VerifyHash, Is.False);
    }

    [Test]
    public void ApplySummary_Format()
    {
        var summary = new ApplySummary
        {
            Databases = 2,
            Applied = 3,
            Skipped = 1,
            Errors = 0
        };

        Assert.That(summary.ToString(), Is.EqualTo("databases=2 applied=3 skipped=1 errors=0"));
    }

    [Test]
    public void StoreSql_DefaultTrue()
    {
        var cfg = new Config([
            "--host", "h", "--port", "5432", "--user", "u", "--password", "p",
            "--scripts", ScriptsDir
        ]);

        Assert.That(cfg.StoreSql, Is.True);
    }

    [Test]
    public void StoreSqlFalse_DisablesSqlStorage()
    {
        var cfg = new Config([
            "--host", "h", "--port", "5432", "--user", "u", "--password", "p",
            "--scripts", ScriptsDir,
            "--store-sql", "false"
        ]);

        Assert.That(cfg.StoreSql, Is.False);
    }
}
