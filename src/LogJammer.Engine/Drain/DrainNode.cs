namespace LogJammer.Engine.Drain;

public class DrainNode
{
    public Dictionary<string, DrainNode> Children { get; set; } = [];
    public List<LogCluster> Clusters { get; set; } = [];
}
