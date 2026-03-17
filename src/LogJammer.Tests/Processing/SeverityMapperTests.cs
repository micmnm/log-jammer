using LogJammer.Engine.Data.Entities;
using LogJammer.Engine.Processing;
using Xunit;

namespace LogJammer.Tests.Processing;

public class SeverityMapperTests
{
    [Theory]
    [InlineData("debug",    Severity.Info)]
    [InlineData("trace",    Severity.Info)]
    [InlineData("verbose",  Severity.Info)]
    [InlineData("info",     Severity.Info)]
    [InlineData("information", Severity.Info)]
    [InlineData("warn",     Severity.Warning)]
    [InlineData("WARNING",  Severity.Warning)]
    [InlineData("error",    Severity.Error)]
    [InlineData("ERROR",    Severity.Error)]
    [InlineData("fatal",    Severity.Critical)]
    [InlineData("CRITICAL", Severity.Critical)]
    [InlineData(null,       Severity.Info)]
    [InlineData("",         Severity.Info)]
    [InlineData("unknown",  Severity.Info)]
    public void Map_ReturnsExpectedSeverity(string? level, Severity expected)
    {
        var result = SeverityMapper.Map(level);
        Assert.Equal(expected, result);
    }
}
