using LogJammer.Engine.Drain;
using Xunit;

namespace LogJammer.Tests.Drain;

public class DrainParserTests
{
    [Fact]
    public void FirstMessage_CreatesNewCluster()
    {
        var parser = new DrainParser();

        var result = parser.ParseLogMessage("Connection to db-primary:5432 timed out after 3000ms");

        Assert.True(result.IsNewCluster);
        Assert.True(result.ClusterId > 0);
        Assert.NotEmpty(result.Template);
    }

    [Fact]
    public void SimilarMessages_MatchSameCluster()
    {
        var parser = new DrainParser();

        var result1 = parser.ParseLogMessage("User alice logged in from 192.168.1.10");
        var result2 = parser.ParseLogMessage("User bob logged in from 10.0.0.5");

        Assert.True(result1.IsNewCluster);
        Assert.False(result2.IsNewCluster);
        Assert.Equal(result1.ClusterId, result2.ClusterId);
        Assert.Contains("*", result2.Template);
    }

    [Fact]
    public void DifferentMessages_CreateDifferentClusters()
    {
        var parser = new DrainParser();

        var result1 = parser.ParseLogMessage("Connection to db-primary:5432 timed out after 3000ms");
        var result2 = parser.ParseLogMessage("Starting application server on port 8080");

        Assert.True(result1.IsNewCluster);
        Assert.True(result2.IsNewCluster);
        Assert.NotEqual(result1.ClusterId, result2.ClusterId);
    }

    [Fact]
    public void Template_PreservesFixedTokens_WildcardsVariables()
    {
        var parser = new DrainParser();

        parser.ParseLogMessage("Connection to db-primary:5432 timed out after 3000ms");
        var result = parser.ParseLogMessage("Connection to db-replica:5432 timed out after 5000ms");

        Assert.Equal("Connection to * timed out after *", result.Template);
    }

    [Fact]
    public void IdenticalMessages_MatchSameCluster()
    {
        var parser = new DrainParser();

        var result1 = parser.ParseLogMessage("Service started successfully");
        var result2 = parser.ParseLogMessage("Service started successfully");

        Assert.True(result1.IsNewCluster);
        Assert.False(result2.IsNewCluster);
        Assert.Equal(result1.ClusterId, result2.ClusterId);
        Assert.Equal("Service started successfully", result2.Template);
    }

    [Fact]
    public void MaxClusters_EvictsLRU()
    {
        var config = new DrainConfig { MaxClusters = 3, SimilarityThreshold = 0.4 };
        var parser = new DrainParser(config);

        // Create 3 clusters with different token counts to ensure they don't match
        var r1 = parser.ParseLogMessage("alpha");
        var r2 = parser.ParseLogMessage("beta gamma");
        var r3 = parser.ParseLogMessage("delta epsilon zeta");

        Assert.True(r1.IsNewCluster);
        Assert.True(r2.IsNewCluster);
        Assert.True(r3.IsNewCluster);

        // Touch r2 and r3 so r1 becomes LRU
        parser.ParseLogMessage("beta gamma");
        parser.ParseLogMessage("delta epsilon zeta");

        // Add a 4th cluster — should evict r1 (LRU)
        var r4 = parser.ParseLogMessage("eta theta iota kappa");
        Assert.True(r4.IsNewCluster);

        // r1's cluster was evicted; feeding "alpha" again should create a new cluster
        var r5 = parser.ParseLogMessage("alpha");
        Assert.True(r5.IsNewCluster);
        Assert.NotEqual(r1.ClusterId, r5.ClusterId);
    }

    [Fact]
    public void SerializeAndRestore_PreservesState()
    {
        var parser1 = new DrainParser();

        parser1.ParseLogMessage("Connection to db-primary:5432 timed out after 3000ms");
        parser1.ParseLogMessage("Connection to db-replica:5432 timed out after 5000ms");
        parser1.ParseLogMessage("Service started successfully");

        var state = parser1.GetState();

        var parser2 = new DrainParser();
        parser2.RestoreState(state);

        // Existing clusters should still match
        var result1 = parser2.ParseLogMessage("Connection to db-standby:5432 timed out after 1000ms");
        Assert.False(result1.IsNewCluster);
        Assert.Equal("Connection to * timed out after *", result1.Template);

        var result2 = parser2.ParseLogMessage("Service started successfully");
        Assert.False(result2.IsNewCluster);

        // A new message should still create a new cluster
        var result3 = parser2.ParseLogMessage("Application shutting down gracefully");
        Assert.True(result3.IsNewCluster);
    }
}
