using LogJammer.Engine.Processing;
using Xunit;

namespace LogJammer.Tests.Processing;

public class MessageTemplateApplierTests
{
    [Fact]
    public void Apply_SubstitutesFieldsIntoTemplate()
    {
        var template = "User {userId} logged in from {ip}";
        var fields = new Dictionary<string, string>
        {
            ["userId"] = "42",
            ["ip"] = "192.168.1.1",
        };

        var result = MessageTemplateApplier.Apply(template, fields);

        Assert.Equal("User 42 logged in from 192.168.1.1", result);
    }

    [Fact]
    public void Apply_LeavesUnmatchedPlaceholderAsIs()
    {
        var template = "User {userId} logged in from {ip}";
        var fields = new Dictionary<string, string>
        {
            ["userId"] = "42",
        };

        var result = MessageTemplateApplier.Apply(template, fields);

        Assert.Equal("User 42 logged in from {ip}", result);
    }

    [Fact]
    public void Apply_NullTemplate_ReturnsEntryMessage()
    {
        var entry = new RawLogEntry
        {
            Message = "fallback message",
            Fields = new Dictionary<string, string> { ["key"] = "value" },
        };

        var result = MessageTemplateApplier.Apply(null, entry);

        Assert.Equal("fallback message", result);
    }
}
