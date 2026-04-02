using Codengine.Connectors.Oracle;
using Xunit;

namespace Codengine.Connectors.Tests;

public class PlSqlFormatterTests
{
    private readonly PlSqlFormatter _formatter = new();

    [Fact]
    public void WhenEmptyInputThenReturnsEmptyWithIntegrity()
    {
        var result = _formatter.Format("");

        Assert.Equal("", result.FormattedCode);
        Assert.True(result.IsIntegrityValid);
    }

    [Fact]
    public void WhenSimpleBeginEndThenIndentsBody()
    {
        var input = """
            BEGIN
            NULL;
            END;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("BEGIN", lines[0]);
        Assert.Equal("    NULL;", lines[1]);
        Assert.Equal("END;", lines[2]);
    }

    [Fact]
    public void WhenIfThenElseEndIfThenIndentsBlocks()
    {
        var input = """
            IF v_count > 0 THEN
            x := 1;
            ELSE
            x := 2;
            END IF;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("IF v_count > 0 THEN", lines[0]);
        Assert.Equal("    x := 1;", lines[1]);
        Assert.Equal("ELSE", lines[2]);
        Assert.Equal("    x := 2;", lines[3]);
        Assert.Equal("END IF;", lines[4]);
    }

    [Fact]
    public void WhenElsifThenDedentsAndReindents()
    {
        var input = """
            IF a = 1 THEN
            x := 1;
            ELSIF a = 2 THEN
            x := 2;
            END IF;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("IF a = 1 THEN", lines[0]);
        Assert.Equal("    x := 1;", lines[1]);
        Assert.Equal("ELSIF a = 2 THEN", lines[2]);
        Assert.Equal("    x := 2;", lines[3]);
        Assert.Equal("END IF;", lines[4]);
    }

    [Fact]
    public void WhenLoopThenIndentsBody()
    {
        var input = """
            LOOP
            EXIT WHEN v_done;
            END LOOP;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("LOOP", lines[0]);
        Assert.Equal("    EXIT WHEN v_done;", lines[1]);
        Assert.Equal("END LOOP;", lines[2]);
    }

    [Fact]
    public void WhenExceptionBlockThenIndentsHandlers()
    {
        var input = """
            BEGIN
            NULL;
            EXCEPTION
            WHEN OTHERS THEN
            RAISE;
            END;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("BEGIN", lines[0]);
        Assert.Equal("    NULL;", lines[1]);
        Assert.Equal("EXCEPTION", lines[2]);
        Assert.Equal("    WHEN OTHERS THEN", lines[3]);
        Assert.Equal("        RAISE;", lines[4]);
        Assert.Equal("END;", lines[5]);
    }

    [Fact]
    public void WhenStringContainsKeywordThenDoesNotAffectIndentation()
    {
        var input = """
            BEGIN
            v_sql := 'BEGIN NULL; END;';
            END;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("BEGIN", lines[0]);
        Assert.Equal("    v_sql := 'BEGIN NULL; END;';", lines[1]);
        Assert.Equal("END;", lines[2]);
    }

    [Fact]
    public void WhenLineCommentContainsKeywordThenDoesNotAffectIndentation()
    {
        var input = """
            BEGIN
            -- END of processing
            NULL;
            END;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("BEGIN", lines[0]);
        Assert.Equal("    -- END of processing", lines[1]);
        Assert.Equal("    NULL;", lines[2]);
        Assert.Equal("END;", lines[3]);
    }

    [Fact]
    public void WhenBlockCommentThenPreservedAndIndented()
    {
        var input = """
            BEGIN
            /* this is
               a multi-line comment */
            NULL;
            END;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        Assert.Contains("/* this is", result.FormattedCode);
        Assert.Contains("a multi-line comment */", result.FormattedCode);
    }

    [Fact]
    public void WhenUppercaseKeywordsEnabledThenKeywordsAreUppercased()
    {
        var formatter = new PlSqlFormatter(new PlSqlFormatterOptions { UppercaseKeywords = true });

        var input = """
            begin
            null;
            end;
            """;

        var result = formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("BEGIN", lines[0]);
        Assert.Equal("    NULL;", lines[1]);
        Assert.Equal("END;", lines[2]);
    }

    [Fact]
    public void WhenUppercaseKeywordsDisabledThenKeywordsUnchanged()
    {
        var formatter = new PlSqlFormatter(new PlSqlFormatterOptions { UppercaseKeywords = false });

        var input = """
            begin
            null;
            end;
            """;

        var result = formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("begin", lines[0]);
        Assert.Equal("    null;", lines[1]);
        Assert.Equal("end;", lines[2]);
    }

    [Fact]
    public void WhenCustomIndentSizeThenUsesSpecifiedSize()
    {
        var formatter = new PlSqlFormatter(new PlSqlFormatterOptions { IndentSize = 2 });

        var input = """
            BEGIN
            NULL;
            END;
            """;

        var result = formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("  NULL;", lines[1]);
    }

    [Fact]
    public void WhenMultipleBlankLinesThenReducedToMax()
    {
        var input = "BEGIN\n\n\n\nNULL;\nEND;";

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        // Default maxConsecutiveBlankLines = 1
        Assert.DoesNotContain("\n\n\n", result.FormattedCode);
    }

    [Fact]
    public void WhenTrailingWhitespaceThenTrimmed()
    {
        var input = "BEGIN   \n    NULL;   \nEND;   ";

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        foreach (var line in result.FormattedCode.Split('\n'))
        {
            Assert.Equal(line.TrimEnd(), line);
        }
    }

    [Fact]
    public void WhenProcedureWithIsBlockThenIndents()
    {
        var input = """
            PROCEDURE do_something(p_id IN NUMBER) IS
            v_count NUMBER;
            BEGIN
            NULL;
            END do_something;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("PROCEDURE do_something(p_id IN NUMBER) IS", lines[0]);
        Assert.Equal("    v_count NUMBER;", lines[1]);
        Assert.Equal("    BEGIN", lines[2]);
        Assert.Equal("        NULL;", lines[3]);
        Assert.Equal("    END do_something;", lines[4]);
    }

    [Fact]
    public void WhenCreatePackageBodyThenIndentsEntireStructure()
    {
        var input = """
            CREATE OR REPLACE PACKAGE BODY PKG_TEST AS
            PROCEDURE my_proc IS
            BEGIN
            NULL;
            END my_proc;
            END PKG_TEST;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("CREATE OR REPLACE PACKAGE BODY PKG_TEST AS", lines[0]);
        Assert.StartsWith("    ", lines[1]); // PROCEDURE indented
    }

    [Fact]
    public void WhenQuotedIdentifierContainsKeywordThenNotAffected()
    {
        var input = """
            BEGIN
            SELECT "BEGIN", "END" FROM dual;
            END;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        Assert.Contains("\"BEGIN\"", result.FormattedCode);
        Assert.Contains("\"END\"", result.FormattedCode);
    }

    [Fact]
    public void WhenKeywordInsideStringLiteralThenNotUppercased()
    {
        var formatter = new PlSqlFormatter(new PlSqlFormatterOptions { UppercaseKeywords = true });

        var input = """
            BEGIN
            v_msg := 'begin processing';
            END;
            """;

        var result = formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        Assert.Contains("'begin processing'", result.FormattedCode);
    }

    [Fact]
    public void WhenEscapedQuoteInStringThenHandledCorrectly()
    {
        var input = """
            BEGIN
            v_msg := 'it''s a test';
            END;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        Assert.Contains("it''s a test", result.FormattedCode);
    }

    [Fact]
    public void WhenNullInputThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _formatter.Format(null!));
    }

    [Fact]
    public void WhenIntegrityCheckThenContentPreserved()
    {
        var input = """
            CREATE OR REPLACE PACKAGE BODY PKG_EXAMPLE AS
            PROCEDURE do_something(p_id IN NUMBER) IS
            v_count NUMBER;
            BEGIN
            SELECT COUNT(*) INTO v_count FROM my_table WHERE id = p_id;
            IF v_count > 0 THEN
            UPDATE my_table SET status = 'DONE' WHERE id = p_id;
            ELSE
            INSERT INTO my_table(id, status) VALUES(p_id, 'NEW');
            END IF;
            EXCEPTION
            WHEN OTHERS THEN
            RAISE;
            END do_something;
            END PKG_EXAMPLE;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
    }

    [Fact]
    public void WhenForLoopThenIndentsBody()
    {
        var input = """
            FOR i IN 1..10 LOOP
            DBMS_OUTPUT.PUT_LINE(i);
            END LOOP;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("FOR i IN 1..10 LOOP", lines[0]);
        Assert.Equal("    DBMS_OUTPUT.PUT_LINE(i);", lines[1]);
        Assert.Equal("END LOOP;", lines[2]);
    }

    [Fact]
    public void WhenStandaloneThenOnSeparateLineThenIndentsBody()
    {
        var input = """
            IF (v_count > 0)
            THEN
            x := 1;
            END IF;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("IF (v_count > 0)", lines[0]);
        Assert.Equal("THEN", lines[1]);
        Assert.Equal("    x := 1;", lines[2]);
        Assert.Equal("END IF;", lines[3]);
    }

    [Fact]
    public void WhenStandaloneThenWithElseThenIndentsCorrectly()
    {
        var input = """
            IF (v_count > 0)
            THEN
            x := 1;
            ELSE
            x := 2;
            END IF;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("IF (v_count > 0)", lines[0]);
        Assert.Equal("THEN", lines[1]);
        Assert.Equal("    x := 1;", lines[2]);
        Assert.Equal("ELSE", lines[3]);
        Assert.Equal("    x := 2;", lines[4]);
        Assert.Equal("END IF;", lines[5]);
    }

    [Fact]
    public void WhenElsifWithStandaloneThenThenIndentsCorrectly()
    {
        var input = """
            IF (a = 1)
            THEN
            x := 1;
            ELSIF (a = 2)
            OR (a = 3)
            THEN
            x := 2;
            ELSE
            x := 3;
            END IF;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("IF (a = 1)", lines[0]);
        Assert.Equal("THEN", lines[1]);
        Assert.Equal("    x := 1;", lines[2]);
        Assert.Equal("ELSIF (a = 2)", lines[3]);
        Assert.Equal("OR (a = 3)", lines[4]);
        Assert.Equal("THEN", lines[5]);
        Assert.Equal("    x := 2;", lines[6]);
        Assert.Equal("ELSE", lines[7]);
        Assert.Equal("    x := 3;", lines[8]);
        Assert.Equal("END IF;", lines[9]);
    }

    [Fact]
    public void WhenMultiLineProcedureWithCloseParenIsThenIndentsBody()
    {
        var input = """
            PROCEDURE my_proc(p_id IN NUMBER,
            p_name IN VARCHAR2) IS
            v_count NUMBER;
            BEGIN
            NULL;
            END my_proc;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("PROCEDURE my_proc(p_id IN NUMBER,", lines[0]);
        Assert.Equal("                  p_name IN VARCHAR2) IS", lines[1]);
        Assert.Equal("    v_count NUMBER;", lines[2]);
        Assert.Equal("    BEGIN", lines[3]);
        Assert.Equal("        NULL;", lines[4]);
        Assert.Equal("    END my_proc;", lines[5]);
    }

    [Fact]
    public void WhenMultiLineFunctionWithReturnIsThenIndentsBody()
    {
        var input = """
            FUNCTION my_func(p_id IN NUMBER,
            p_name IN VARCHAR2) RETURN NUMBER IS
            v_result NUMBER;
            BEGIN
            v_result := 1;
            RETURN v_result;
            END my_func;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("FUNCTION my_func(p_id IN NUMBER,", lines[0]);
        Assert.Equal("                 p_name IN VARCHAR2) RETURN NUMBER IS", lines[1]);
        Assert.Equal("    v_result NUMBER;", lines[2]);
        Assert.Equal("    BEGIN", lines[3]);
        Assert.Equal("        v_result := 1;", lines[4]);
        Assert.Equal("        RETURN v_result;", lines[5]);
        Assert.Equal("    END my_func;", lines[6]);
    }

    [Fact]
    public void WhenEndClosesBeginAndSubProgramThenStackIsClean()
    {
        var input = """
            CREATE OR REPLACE PACKAGE BODY PKG_TEST AS
            PROCEDURE proc_a(p_id IN NUMBER,
            p_name IN VARCHAR2) IS
            BEGIN
            NULL;
            END proc_a;
            PROCEDURE proc_b IS
            BEGIN
            NULL;
            END proc_b;
            END PKG_TEST;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("CREATE OR REPLACE PACKAGE BODY PKG_TEST AS", lines[0]);
        // proc_a: multi-line with ) IS
        Assert.Equal("    PROCEDURE proc_a(p_id IN NUMBER,", lines[1]);
        Assert.Equal("                     p_name IN VARCHAR2) IS", lines[2]);
        Assert.Equal("        BEGIN", lines[3]);
        Assert.Equal("            NULL;", lines[4]);
        Assert.Equal("        END proc_a;", lines[5]);
        // proc_b: single-line IS — should be at same level as proc_a
        Assert.Equal("    PROCEDURE proc_b IS", lines[6]);
        Assert.Equal("        BEGIN", lines[7]);
        Assert.Equal("            NULL;", lines[8]);
        Assert.Equal("        END proc_b;", lines[9]);
        Assert.Equal("END PKG_TEST;", lines[10]);
    }

    [Fact]
    public void WhenMultipleProceduresInPackageThenIndentationDoesNotDrift()
    {
        var input = """
            CREATE OR REPLACE PACKAGE BODY PKG_TEST AS
            PROCEDURE proc_a IS
            BEGIN
            NULL;
            END proc_a;
            PROCEDURE proc_b IS
            BEGIN
            NULL;
            END proc_b;
            PROCEDURE proc_c IS
            BEGIN
            NULL;
            END proc_c;
            END PKG_TEST;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        // All three procedures should be at same indent level (4 spaces)
        Assert.Equal("    PROCEDURE proc_a IS", lines[1]);
        Assert.Equal("    PROCEDURE proc_b IS", lines[5]);
        Assert.Equal("    PROCEDURE proc_c IS", lines[9]);
        Assert.Equal("END PKG_TEST;", lines[13]);
    }

    [Fact]
    public void WhenNestedIfWithStandaloneThenInsideProcedureThenIndentsCorrectly()
    {
        var input = """
            PROCEDURE my_proc IS
            BEGIN
            IF (a IS NULL)
            THEN
            a := 1;
            ELSE
            a := 2;
            END IF;
            IF (b IS NULL)
            THEN
            b := 1;
            END IF;
            END my_proc;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("PROCEDURE my_proc IS", lines[0]);
        Assert.Equal("    BEGIN", lines[1]);
        Assert.Equal("        IF (a IS NULL)", lines[2]);
        Assert.Equal("        THEN", lines[3]);
        Assert.Equal("            a := 1;", lines[4]);
        Assert.Equal("        ELSE", lines[5]);
        Assert.Equal("            a := 2;", lines[6]);
        Assert.Equal("        END IF;", lines[7]);
        Assert.Equal("        IF (b IS NULL)", lines[8]);
        Assert.Equal("        THEN", lines[9]);
        Assert.Equal("            b := 1;", lines[10]);
        Assert.Equal("        END IF;", lines[11]);
        Assert.Equal("    END my_proc;", lines[12]);
    }

    [Fact]
    public void WhenProcedureHasMultiLineParamsThenAlignsToOpenParen()
    {
        var input = """
            PROCEDURE p_foo(p_id IN NUMBER,
            p_name IN VARCHAR2,
            p_date IN DATE) IS
            BEGIN
            NULL;
            END p_foo;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        // ( is at position 15, alignment at column 16
        Assert.Equal("PROCEDURE p_foo(p_id IN NUMBER,", lines[0]);
        Assert.Equal("                p_name IN VARCHAR2,", lines[1]);
        Assert.Equal("                p_date IN DATE) IS", lines[2]);
        Assert.Equal("    BEGIN", lines[3]);
        Assert.Equal("        NULL;", lines[4]);
        Assert.Equal("    END p_foo;", lines[5]);
    }

    [Fact]
    public void WhenProcedureInPackageHasMultiLineParamsThenAlignsToOpenParen()
    {
        var input = """
            CREATE OR REPLACE PACKAGE BODY PKG AS
            PROCEDURE p_bar(p_id IN NUMBER,
            p_name IN VARCHAR2) IS
            BEGIN
            NULL;
            END p_bar;
            END PKG;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("CREATE OR REPLACE PACKAGE BODY PKG AS", lines[0]);
        // ( is at position 19 (4 + 15), alignment at column 20
        Assert.Equal("    PROCEDURE p_bar(p_id IN NUMBER,", lines[1]);
        Assert.Equal("                    p_name IN VARCHAR2) IS", lines[2]);
        Assert.Equal("        BEGIN", lines[3]);
        Assert.Equal("            NULL;", lines[4]);
        Assert.Equal("        END p_bar;", lines[5]);
        Assert.Equal("END PKG;", lines[6]);
    }

    [Fact]
    public void WhenParensBalancedOnSameLineThenNoAlignment()
    {
        var input = """
            PROCEDURE do_something(p_id IN NUMBER) IS
            BEGIN
            NULL;
            END do_something;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("PROCEDURE do_something(p_id IN NUMBER) IS", lines[0]);
        Assert.Equal("    BEGIN", lines[1]);
    }

    [Fact]
    public void WhenCommentInsideParenBlockThenAlignsWithParams()
    {
        var input = """
            PROCEDURE p_foo(p_id IN NUMBER,
            --------------------------------------
            p_name IN VARCHAR2) IS
            BEGIN
            NULL;
            END p_foo;
            """;

        var result = _formatter.Format(input);

        Assert.True(result.IsIntegrityValid);
        var lines = result.FormattedCode.Split('\n');
        Assert.Equal("PROCEDURE p_foo(p_id IN NUMBER,", lines[0]);
        Assert.Equal("                --------------------------------------", lines[1]);
        Assert.Equal("                p_name IN VARCHAR2) IS", lines[2]);
    }
}
