using Migrator;

namespace Tests;

public class SqlStatementSplitter_Tests
{
    [Test]
    public void Splits_SimpleStatements()
    {
        var sql = "create table a(id int); create table b(id int);";
        var parts = SqlStatementSplitter.Split(sql);

        Assert.That(parts, Is.EqualTo(new[]
        {
            "create table a(id int)",
            "create table b(id int)"
        }));
    }

    [Test]
    public void DollarQuoting_DoBlock_NotSplitOnInnerSemicolon()
    {
        var sql = """
                  create table t(id int);
                  do $$
                  begin
                    insert into t values (1);
                    insert into t values (2);
                  end
                  $$;
                  select 1;
                  """;

        var parts = SqlStatementSplitter.Split(sql);

        Assert.That(parts.Count, Is.EqualTo(3));
        Assert.That(parts[0], Does.Contain("create table t"));
        Assert.That(parts[1], Does.StartWith("do $$"));
        Assert.That(parts[1], Does.Contain("insert into t values (1);"));
        Assert.That(parts[1], Does.Contain("end"));
        Assert.That(parts[2], Is.EqualTo("select 1"));
    }

    [Test]
    public void DollarQuoting_NamedTag()
    {
        var sql = "do $body$ begin perform 1; end $body$; select 2;";
        var parts = SqlStatementSplitter.Split(sql);

        Assert.That(parts.Count, Is.EqualTo(2));
        Assert.That(parts[0], Does.Contain("$body$"));
        Assert.That(parts[0], Does.Contain("perform 1;"));
        Assert.That(parts[1], Is.EqualTo("select 2"));
    }

    [Test]
    public void SemicolonInsideSingleQuotes_Ignored()
    {
        var sql = "insert into t values ('a;b'); select 1;";
        var parts = SqlStatementSplitter.Split(sql);

        Assert.That(parts.Count, Is.EqualTo(2));
        Assert.That(parts[0], Does.Contain("'a;b'"));
    }

    [Test]
    public void LineComment_DoesNotBreakOnInnerSemicolon()
    {
        var sql = "select 1; -- comment; still comment\nselect 2;";
        var parts = SqlStatementSplitter.Split(sql);

        // ';' внутри --комментария не разделитель; комментарий относится ко 2-му statement
        Assert.That(parts.Count, Is.EqualTo(2));
        Assert.That(parts[0], Is.EqualTo("select 1"));
        Assert.That(parts[1], Does.Contain("select 2"));
        Assert.That(parts[1], Does.Contain("-- comment; still comment"));
    }

    [Test]
    public void Empty_ReturnsEmpty()
    {
        Assert.That(SqlStatementSplitter.Split("   "), Is.Empty);
    }
}
