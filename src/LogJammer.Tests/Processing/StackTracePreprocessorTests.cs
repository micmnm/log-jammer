using LogJammer.Engine.Processing;
using Xunit;

namespace LogJammer.Tests.Processing;

public class StackTracePreprocessorTests
{
    private static readonly string SampleStackTrace =
        "System.Data.SqlClient.SqlException: Connection timeout\r\n" +
        "   at MyApp.Services.PaymentService.Process(PaymentRequest request) in /app/src/PaymentService.cs:line 42\r\n" +
        "   at MyApp.Data.DatabaseClient.Execute(string sql) in /app/src/DatabaseClient.cs:line 17\r\n" +
        "   at Npgsql.NpgsqlConnection.Open() in /src/Npgsql/NpgsqlConnection.cs:line 99\r\n" +
        "   at MyApp.Middleware.RequestMiddleware.Invoke(HttpContext ctx) in /app/src/Middleware.cs:line 55\r\n" +
        "   at Microsoft.AspNetCore.Hosting.HostingApplication.ProcessRequestAsync(Context ctx)";

    [Fact]
    public void DetectsStackTraceByFieldName()
    {
        var fields = new Dictionary<string, string>
        {
            ["stack_trace"] = SampleStackTrace,
            ["message"] = "Payment failed"
        };

        var result = StackTracePreprocessor.Process(fields);

        // stack_trace field should be cleaned (not equal to original)
        Assert.NotEqual(SampleStackTrace, result["stack_trace"]);
        Assert.StartsWith("at ", result["stack_trace"]);
        // message field should be unchanged
        Assert.Equal("Payment failed", result["message"]);
    }

    [Fact]
    public void IgnoresNonStackTraceFields()
    {
        var content = "   at SomeClass.SomeMethod() in /some/path.cs:line 1";
        var fields = new Dictionary<string, string>
        {
            ["message"] = content
        };

        var result = StackTracePreprocessor.Process(fields);

        Assert.Equal(content, result["message"]);
    }

    [Fact]
    public void ExtractsTop3Frames()
    {
        var fields = new Dictionary<string, string>
        {
            ["stack_trace"] = SampleStackTrace
        };

        var result = StackTracePreprocessor.Process(fields);

        // The summary should contain exactly 2 " > " separators (3 frames)
        var summary = result["stack_trace"];
        Assert.Equal(2, summary.Split(" > ").Length - 1);
    }

    [Fact]
    public void StripsLineNumbersAndPaths()
    {
        var fields = new Dictionary<string, string>
        {
            ["exception"] = SampleStackTrace
        };

        var result = StackTracePreprocessor.Process(fields);
        var cleaned = result["exception"];

        Assert.DoesNotContain("line 42", cleaned);
        Assert.DoesNotContain("/app/src", cleaned);
    }

    [Fact]
    public void FormatsAsSummary()
    {
        var fields = new Dictionary<string, string>
        {
            ["stack_trace"] = SampleStackTrace
        };

        var result = StackTracePreprocessor.Process(fields);

        Assert.Equal(
            "at PaymentService.Process > DatabaseClient.Execute > NpgsqlConnection.Open",
            result["stack_trace"]);
    }
}
