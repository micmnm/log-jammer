namespace LogJammer.Engine.Drain;

public class LogCluster
{
    public int Id { get; set; }
    public List<string> Tokens { get; set; } = [];
    public long MatchCount { get; set; }
    public long LastMatchOrder { get; set; }

    public string GetTemplate() => string.Join(" ", Tokens);
}
