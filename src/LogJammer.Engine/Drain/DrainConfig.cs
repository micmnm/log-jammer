namespace LogJammer.Engine.Drain;

public class DrainConfig
{
    public double SimilarityThreshold { get; set; } = 0.4;
    public int MaxClusters { get; set; } = 1000;
    public int TreeDepth { get; set; } = 4;
}
