using Codengine.Connectors.Oracle;
using Codengine.Connectors.Oracle.Formatting;
using Xunit;

namespace Codengine.Connectors.Tests;

public class FormattingEngineSelectorTests
{
    private readonly FormattingEngineSelector _selector = new();

    [Fact]
    public void WhenBasicModeThenReturnsBasicEngine()
    {
        var engine = _selector.Select("SELECT 1 FROM dual", FormattingEngineMode.Basic);

        Assert.Equal("Basic (built-in)", engine.Name);
    }

    [Fact]
    public void WhenSqlFormatterNetModeThenReturnsSqlFormatterEngine()
    {
        var engine = _selector.Select("SELECT 1 FROM dual", FormattingEngineMode.SqlFormatterNet);

        Assert.Equal("SqlFormatterNet (Hogimn.Sql.Formatter)", engine.Name);
    }

    [Fact]
    public void WhenSqlclModeAndNotAvailableThenFallsBackToBasic()
    {
        var engine = _selector.Select("SELECT 1 FROM dual", FormattingEngineMode.Sqlcl);

        Assert.Equal("Basic (built-in)", engine.Name);
    }

    [Fact]
    public void WhenAutoModeAndPlSqlThenSelectsBasicEngine()
    {
        var sql = "CREATE OR REPLACE PACKAGE BODY PKG_TEST AS\nBEGIN\nNULL;\nEND;\nEND PKG_TEST;";

        var engine = _selector.Select(sql, FormattingEngineMode.Auto);

        // Without SQLcl installed, should fall back to Basic for PL/SQL
        Assert.Equal("Basic (built-in)", engine.Name);
    }

    [Fact]
    public void WhenAutoModeAndSimpleSqlThenSelectsSqlFormatterNet()
    {
        var sql = "SELECT id, name FROM users WHERE active = 1 ORDER BY name";

        var engine = _selector.Select(sql, FormattingEngineMode.Auto);

        Assert.Equal("SqlFormatterNet (Hogimn.Sql.Formatter)", engine.Name);
    }

    [Theory]
    [InlineData("CREATE OR REPLACE PACKAGE pkg_test AS END;")]
    [InlineData("CREATE OR REPLACE PACKAGE BODY pkg_test AS END;")]
    [InlineData("CREATE OR REPLACE PROCEDURE my_proc IS BEGIN NULL; END;")]
    [InlineData("CREATE OR REPLACE FUNCTION my_func RETURN NUMBER IS BEGIN RETURN 1; END;")]
    [InlineData("CREATE OR REPLACE TRIGGER my_trigger BEFORE INSERT ON t BEGIN NULL; END;")]
    [InlineData("CREATE OR REPLACE TYPE my_type AS OBJECT (id NUMBER);")]
    public void WhenAutoModeAndProceduralPlSqlThenSelectsBasicEngine(string sql)
    {
        var engine = _selector.Select(sql, FormattingEngineMode.Auto);

        Assert.Equal("Basic (built-in)", engine.Name);
    }

    [Theory]
    [InlineData("SELECT * FROM users")]
    [InlineData("INSERT INTO users VALUES (1, 'test')")]
    [InlineData("UPDATE users SET name = 'test' WHERE id = 1")]
    [InlineData("DELETE FROM users WHERE id = 1")]
    [InlineData("MERGE INTO users USING source ON (1=1) WHEN MATCHED THEN UPDATE SET name = 'x'")]
    public void WhenAutoModeAndSimpleDmlThenSelectsSqlFormatterNet(string sql)
    {
        var engine = _selector.Select(sql, FormattingEngineMode.Auto);

        Assert.Equal("SqlFormatterNet (Hogimn.Sql.Formatter)", engine.Name);
    }

    [Fact]
    public void WhenFormatWithBasicModeThenFormatsCorrectly()
    {
        var sql = "BEGIN\nNULL;\nEND;";
        var options = new PlSqlFormatterOptions();

        var result = _selector.Format(sql, options, FormattingEngineMode.Basic);

        Assert.Contains("    NULL;", result.FormattedCode);
        Assert.Equal("Basic (built-in)", result.EngineName);
        Assert.False(result.FallbackUsed);
    }

    [Fact]
    public void WhenFormatWithAutoModeAndPlSqlThenUsesBasicEngine()
    {
        var sql = """
            CREATE OR REPLACE PACKAGE BODY PKG_TEST AS
            PROCEDURE my_proc IS
            BEGIN
            NULL;
            END my_proc;
            END PKG_TEST;
            """;
        var options = new PlSqlFormatterOptions();

        var result = _selector.Format(sql, options, FormattingEngineMode.Auto);

        Assert.Equal("Basic (built-in)", result.EngineName);
        Assert.False(result.FallbackUsed);
    }
}

public class BasicPlSqlFormattingEngineTests
{
    private readonly BasicPlSqlFormattingEngine _engine = new();

    [Fact]
    public void WhenNameThenReturnsExpectedName()
    {
        Assert.Equal("Basic (built-in)", _engine.Name);
    }

    [Fact]
    public void WhenIsAvailableThenReturnsTrue()
    {
        Assert.True(_engine.IsAvailable);
    }

    [Fact]
    public void WhenFormatSimpleBlockThenIndentsCorrectly()
    {
        var sql = "BEGIN\nNULL;\nEND;";
        var options = new PlSqlFormatterOptions();

        var result = _engine.Format(sql, options);

        var lines = result.Split('\n');
        Assert.Equal("BEGIN", lines[0]);
        Assert.Equal("    NULL;", lines[1]);
        Assert.Equal("END;", lines[2]);
    }

    [Fact]
    public void WhenNullInputThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.Format(null!, new PlSqlFormatterOptions()));
    }

    [Fact]
    public void WhenCustomIndentSizeThenUsesIt()
    {
        var sql = "BEGIN\nNULL;\nEND;";
        var options = new PlSqlFormatterOptions { IndentSize = 2 };

        var result = _engine.Format(sql, options);

        Assert.Contains("  NULL;", result);
    }
}

public class SqlFormatterNetEngineTests
{
    private readonly SqlFormatterNetEngine _engine = new();

    [Fact]
    public void WhenNameThenReturnsExpectedName()
    {
        Assert.Equal("SqlFormatterNet (Hogimn.Sql.Formatter)", _engine.Name);
    }

    [Fact]
    public void WhenIsAvailableThenReturnsTrue()
    {
        Assert.True(_engine.IsAvailable);
    }

    [Fact]
    public void WhenFormatSimpleSelectThenFormatsWithIndentation()
    {
        var sql = "SELECT id, name, email FROM users WHERE active = 1 AND role = 'admin' ORDER BY name";
        var options = new PlSqlFormatterOptions { IndentSize = 4 };

        var result = _engine.Format(sql, options);

        Assert.Contains("SELECT", result);
        Assert.Contains("FROM", result);
        Assert.Contains("WHERE", result);
    }

    [Fact]
    public void WhenUppercaseKeywordsEnabledThenKeywordsUppercased()
    {
        var sql = "select id from users where active = 1";
        var options = new PlSqlFormatterOptions { UppercaseKeywords = true };

        var result = _engine.Format(sql, options);

        Assert.Contains("SELECT", result);
        Assert.Contains("FROM", result);
        Assert.Contains("WHERE", result);
    }

    [Fact]
    public void WhenUppercaseKeywordsDisabledThenKeywordsPreserved()
    {
        var sql = "select id from users where active = 1";
        var options = new PlSqlFormatterOptions { UppercaseKeywords = false };

        var result = _engine.Format(sql, options);

        Assert.Contains("select", result);
    }

    [Fact]
    public void WhenNullInputThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.Format(null!, new PlSqlFormatterOptions()));
    }

    [Fact]
    public void WhenTrimTrailingWhitespaceEnabledThenNoTrailingSpaces()
    {
        var sql = "SELECT id, name FROM users WHERE active = 1 ORDER BY name";
        var options = new PlSqlFormatterOptions { TrimTrailingWhitespace = true };

        var result = _engine.Format(sql, options);

        foreach (var line in result.Split('\n'))
        {
            Assert.Equal(line.TrimEnd(), line);
        }
    }
}

public class SqlclFormattingEngineTests
{
    [Fact]
    public void WhenNameThenReturnsExpectedName()
    {
        var engine = new SqlclFormattingEngine(null);

        Assert.Equal("SQLcl (Oracle)", engine.Name);
    }

    [Fact]
    public void WhenNullPathThenNotAvailable()
    {
        var engine = new SqlclFormattingEngine(null);

        Assert.False(engine.IsAvailable);
    }

    [Fact]
    public void WhenInvalidPathThenNotAvailable()
    {
        var engine = new SqlclFormattingEngine(@"C:\nonexistent\sql.exe");

        Assert.False(engine.IsAvailable);
    }

    [Fact]
    public void WhenNotAvailableAndFormatCalledThenThrowsInvalidOperationException()
    {
        var engine = new SqlclFormattingEngine(null);

        Assert.Throws<InvalidOperationException>(() =>
            engine.Format("SELECT 1 FROM dual", new PlSqlFormatterOptions()));
    }
}

public class CombinedPlSqlFormattingEngineTests
{
    private readonly CombinedPlSqlFormattingEngine _engine = new();

    [Fact]
    public void WhenNameThenReturnsExpectedName()
    {
        Assert.Equal("Combined (Basic + SqlFormatterNet)", _engine.Name);
    }

    [Fact]
    public void WhenIsAvailableThenReturnsTrue()
    {
        Assert.True(_engine.IsAvailable);
    }

    [Fact]
    public void WhenNullInputThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.Format(null!, new PlSqlFormatterOptions()));
    }

    [Fact]
    public void WhenSimpleBlockThenIndentsCorrectly()
    {
        var sql = "BEGIN\nNULL;\nEND;";
        var options = new PlSqlFormatterOptions();

        var result = _engine.Format(sql, options);

        var lines = result.Split('\n');
        Assert.Equal("BEGIN", lines[0]);
        Assert.Contains("NULL;", lines[1]);
        Assert.Equal("END;", lines[2]);
    }

    [Fact]
    public void WhenPackageWithEmbeddedSelectThenFormatsQueryWithinBlock()
    {
        var sql = """
            CREATE OR REPLACE PACKAGE BODY PKG_TEST AS
            PROCEDURE my_proc IS
            v_count NUMBER;
            BEGIN
            SELECT COUNT(*) INTO v_count FROM users WHERE email = 'test@test.com';
            END my_proc;
            END PKG_TEST;
            """;
        var options = new PlSqlFormatterOptions();

        var result = _engine.Format(sql, options);

        // Block indentation from Basic should be present
        Assert.Contains("PROCEDURE", result);
        Assert.Contains("BEGIN", result);
        Assert.Contains("END my_proc;", result);
        // The SELECT should be formatted (SqlFormatterNet may split it across lines)
        Assert.Contains("SELECT", result);
        Assert.Contains("FROM", result);
    }

    [Fact]
    public void WhenPackageWithInsertThenFormatsInsertStatement()
    {
        var sql = """
            CREATE OR REPLACE PACKAGE BODY PKG_TEST AS
            PROCEDURE my_proc IS
            BEGIN
            INSERT INTO users(name, email) VALUES('John', 'john@test.com');
            END my_proc;
            END PKG_TEST;
            """;
        var options = new PlSqlFormatterOptions();

        var result = _engine.Format(sql, options);

        Assert.Contains("INSERT", result);
        Assert.Contains("VALUES", result);
        Assert.Contains("BEGIN", result);
        Assert.Contains("END my_proc;", result);
    }

    [Fact]
    public void WhenCommentContainsSqlKeywordThenPreservesComment()
    {
        var sql = """
            BEGIN
            -- SELECT this is a comment
            NULL;
            END;
            """;
        var options = new PlSqlFormatterOptions();

        var result = _engine.Format(sql, options);

        Assert.Contains("-- SELECT this is a comment", result);
    }

    [Fact]
    public void WhenBlockCommentContainsSqlKeywordThenPreservesComment()
    {
        var sql = """
            BEGIN
            /* SELECT * FROM users */
            NULL;
            END;
            """;
        var options = new PlSqlFormatterOptions();

        var result = _engine.Format(sql, options);

        Assert.Contains("/* SELECT * FROM users */", result);
    }

    [Fact]
    public void WhenSelectorWithCombinedModeThenReturnsCombinedEngine()
    {
        var selector = new FormattingEngineSelector();

        var engine = selector.Select("SELECT 1 FROM dual", FormattingEngineMode.Combined);

        Assert.Equal("Combined (Basic + SqlFormatterNet)", engine.Name);
    }

    [Fact]
    public void WhenFormatWithCombinedModeThenFormatsCorrectly()
    {
        var selector = new FormattingEngineSelector();
        var sql = "BEGIN\nSELECT id, name FROM users WHERE active = 1;\nEND;";
        var options = new PlSqlFormatterOptions();

        var result = selector.Format(sql, options, FormattingEngineMode.Combined);

        Assert.Equal("Combined (Basic + SqlFormatterNet)", result.EngineName);
        Assert.False(result.FallbackUsed);
        Assert.Contains("SELECT", result.FormattedCode);
        Assert.Contains("FROM", result.FormattedCode);
    }
}
