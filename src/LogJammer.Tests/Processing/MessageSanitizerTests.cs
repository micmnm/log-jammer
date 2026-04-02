using LogJammer.Engine.Processing;
using Xunit;

namespace LogJammer.Tests.Processing;

public class MessageSanitizerTests
{
    // -------------------------------------------------------------------------
    // URLs
    // -------------------------------------------------------------------------

    [Fact]
    public void ReplacesHttpsUrl()
    {
        var result = MessageSanitizer.Sanitize("https://api.example.com/v2/users/123");
        Assert.Equal("<url>", result);
    }

    [Fact]
    public void ReplacesHttpUrl()
    {
        var result = MessageSanitizer.Sanitize("http://localhost:3000");
        Assert.Equal("<url>", result);
    }

    [Fact]
    public void ReplacesUrlInSentence()
    {
        var result = MessageSanitizer.Sanitize("Request to https://api.example.com/resource failed");
        Assert.Equal("Request to <url> failed", result);
    }

    // -------------------------------------------------------------------------
    // GUIDs
    // -------------------------------------------------------------------------

    [Fact]
    public void ReplacesStandaloneGuid()
    {
        var result = MessageSanitizer.Sanitize("Processing d61652a8-682d-4f55-9ff9-004ae52723e6");
        Assert.Equal("Processing <guid>", result);
    }

    [Fact]
    public void ReplacesGuidInsidePath()
    {
        // GUID is replaced first, then path still matches the surrounding structure
        var result = MessageSanitizer.Sanitize("/apps/data/d61652a8-682d-4f55-9ff9-004ae52723e6");
        Assert.Equal("<path>", result);
    }

    // -------------------------------------------------------------------------
    // File paths
    // -------------------------------------------------------------------------

    [Fact]
    public void ReplacesUnixPath()
    {
        var result = MessageSanitizer.Sanitize("/apps/druidkbnetagent/App_Data/Pandoc/663/file.txt");
        Assert.Equal("<path>", result);
    }

    [Fact]
    public void ReplacesShortUnixPath()
    {
        var result = MessageSanitizer.Sanitize("/tmp/something.lua");
        Assert.Equal("<path>", result);
    }

    [Fact]
    public void ReplacesWindowsPath()
    {
        var result = MessageSanitizer.Sanitize(@"C:\Windows\System32\file.dll");
        Assert.Equal("<path>", result);
    }

    [Fact]
    public void ReplacesUnixPathInMessage()
    {
        var result = MessageSanitizer.Sanitize("Loading config from /etc/myapp/config.yaml done");
        Assert.Equal("Loading config from <path> done", result);
    }

    // -------------------------------------------------------------------------
    // Key=value args
    // -------------------------------------------------------------------------

    [Fact]
    public void ReplacesKeyValueWithPath()
    {
        // The path inside the value is replaced at the path step,
        // but the --flag= part should be covered by key=value step
        var result = MessageSanitizer.Sanitize("--extract-media=/apps/foo");
        Assert.Equal("--extract-media=<value>", result);
    }

    [Fact]
    public void ReplacesKeyValueWithLuaFilter()
    {
        var result = MessageSanitizer.Sanitize("--lua-filter=/tmp/file.lua");
        Assert.Equal("--lua-filter=<value>", result);
    }

    [Fact]
    public void DoesNotReplaceKeyValueWithSimpleWord_Html()
    {
        var result = MessageSanitizer.Sanitize("--from=html");
        Assert.Equal("--from=html", result);
    }

    [Fact]
    public void DoesNotReplaceKeyValueWithSimpleWord_None()
    {
        var result = MessageSanitizer.Sanitize("--wrap=none");
        Assert.Equal("--wrap=none", result);
    }

    [Fact]
    public void DoesNotReplaceKeyValueWithSimpleWord_Asciidoc()
    {
        var result = MessageSanitizer.Sanitize("--to=asciidoc");
        Assert.Equal("--to=asciidoc", result);
    }

    // -------------------------------------------------------------------------
    // Standalone numbers
    // -------------------------------------------------------------------------

    [Fact]
    public void ReplacesLargeNumber()
    {
        var result = MessageSanitizer.Sanitize("805306368");
        Assert.Equal("<num>", result);
    }

    [Fact]
    public void ReplacesSmallNumber()
    {
        var result = MessageSanitizer.Sanitize("768");
        Assert.Equal("<num>", result);
    }

    [Fact]
    public void ReplacesNumberInParentheses()
    {
        var result = MessageSanitizer.Sanitize("PandocUnknownError (251)");
        Assert.Equal("PandocUnknownError (<num>)", result);
    }

    [Fact]
    public void ReplacesMillisecondSuffix()
    {
        var result = MessageSanitizer.Sanitize("Request took 3000ms");
        Assert.Equal("Request took <num>ms", result);
    }

    [Fact]
    public void ReplacesMegabyteNumber()
    {
        // The regex consumes whitespace between the number and the unit suffix
        var result = MessageSanitizer.Sanitize("768 MB");
        Assert.Equal("<num>MB", result);
    }

    [Fact]
    public void DoesNotReplaceVersionLikeV2()
    {
        var result = MessageSanitizer.Sanitize("v2");
        Assert.Equal("v2", result);
    }

    [Fact]
    public void DoesNotReplaceLog4j()
    {
        var result = MessageSanitizer.Sanitize("log4j");
        Assert.Equal("log4j", result);
    }

    // -------------------------------------------------------------------------
    // No false positives
    // -------------------------------------------------------------------------

    [Fact]
    public void PlainTextPassesThroughUnchanged()
    {
        var input = "Service started successfully";
        Assert.Equal(input, MessageSanitizer.Sanitize(input));
    }

    [Fact]
    public void MessageWithOnlyWordsUnchanged()
    {
        var input = "Druid.KnowledgeBase.DotNet.Common | Heap exhausted";
        Assert.Equal(input, MessageSanitizer.Sanitize(input));
    }

    // -------------------------------------------------------------------------
    // Full pandoc message
    // -------------------------------------------------------------------------

    [Fact]
    public void SanitizesFullPandocMessage()
    {
        var input =
            "Druid.KnowledgeBase.DotNet.Common | Druid.KbNet.Agent.ExtractorWorker | PandocUnknownError (251): /pandoc-3.5/bin/pandoc --extract-media=/apps/druidkbnetagent/App_Data/Pandoc/663/d61652a8-682d-4f55-9ff9-004ae52723e6 --lua-filter=/tmp/content_filtering_a0729116-a867-4ba2-b78b-e2f5eb5c0c4d.lua +RTS -M768M -RTS --output=- --from=html --to=asciidoc --wrap=none\npandoc: Heap exhausted;\npandoc: Current maximum heap size is 805306368 bytes (768 MB).\npandoc: Use `+RTS -M<size>' to increase it.\n | Extracting content from url {WebsitePage} failed. (WebsitePageId: {WebsitePageId}) | Error";

        var result = MessageSanitizer.Sanitize(input);

        // URLs should be replaced
        Assert.DoesNotContain("https://", result);
        Assert.DoesNotContain("http://", result);

        // GUIDs should be replaced
        Assert.DoesNotContain("d61652a8-682d-4f55-9ff9-004ae52723e6", result);
        Assert.DoesNotContain("a0729116-a867-4ba2-b78b-e2f5eb5c0c4d", result);

        // File paths should be replaced
        Assert.DoesNotContain("/pandoc-3.5/bin/pandoc", result);
        Assert.DoesNotContain("/apps/druidkbnetagent", result);
        Assert.DoesNotContain("/tmp/content_filtering", result);

        // Complex key=value args should be replaced
        Assert.DoesNotContain("--extract-media=/apps", result);
        Assert.DoesNotContain("--lua-filter=/tmp", result);
        Assert.Contains("--extract-media=<value>", result);
        Assert.Contains("--lua-filter=<value>", result);

        // Simple key=value args should be preserved
        Assert.Contains("--from=html", result);
        Assert.Contains("--to=asciidoc", result);
        Assert.Contains("--wrap=none", result);

        // Large numbers should be replaced
        Assert.DoesNotContain("805306368", result);
        Assert.Contains("<num>", result);

        // Plain words and structure preserved
        Assert.Contains("Druid.KnowledgeBase.DotNet.Common", result);
        Assert.Contains("Druid.KbNet.Agent.ExtractorWorker", result);
        Assert.Contains("PandocUnknownError", result);
        Assert.Contains("Heap exhausted", result);
        Assert.Contains("--output=-", result);
        Assert.Contains("{WebsitePage}", result);
        Assert.Contains("{WebsitePageId}", result);
        Assert.Contains("Error", result);
    }
}
