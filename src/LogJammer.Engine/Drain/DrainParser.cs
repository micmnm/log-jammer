using System.Text.Json;
using System.Text.RegularExpressions;

namespace LogJammer.Engine.Drain;

public partial class DrainParser
{
    private const string WildcardToken = "*";

    private readonly DrainConfig _config;
    private readonly DrainNode _root = new();
    private readonly List<LogCluster> _clusters = [];
    private int _nextClusterId = 1;
    private long _matchOrder;

    public DrainParser(DrainConfig? config = null)
    {
        _config = config ?? new DrainConfig();
    }

    public DrainResult ParseLogMessage(string message)
    {
        var tokens = Tokenize(message);
        if (tokens.Length == 0)
        {
            return new DrainResult(0, string.Empty, true);
        }

        // Search all candidate leaf nodes for the best matching cluster
        var candidateLeaves = FindCandidateLeaves(tokens);
        LogCluster? bestCluster = null;
        var bestSimilarity = -1.0;

        foreach (var leaf in candidateLeaves)
        {
            var (cluster, similarity) = FindBestMatch(leaf, tokens);
            if (cluster is not null && similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestCluster = cluster;
            }
        }

        if (bestCluster is not null && bestSimilarity >= _config.SimilarityThreshold)
        {
            UpdateTemplate(bestCluster, tokens);
            bestCluster.MatchCount++;
            bestCluster.LastMatchOrder = ++_matchOrder;
            return new DrainResult(bestCluster.Id, bestCluster.GetTemplate(), false);
        }

        if (_clusters.Count >= _config.MaxClusters)
        {
            EvictLruCluster();
        }

        // Insert new cluster into the tree using exact token routing
        var insertLeaf = TraverseTree(tokens);

        var newCluster = new LogCluster
        {
            Id = _nextClusterId++,
            Tokens = [.. tokens],
            MatchCount = 1,
            LastMatchOrder = ++_matchOrder,
        };

        insertLeaf.Clusters.Add(newCluster);
        _clusters.Add(newCluster);

        return new DrainResult(newCluster.Id, newCluster.GetTemplate(), true);
    }

    public byte[] GetState()
    {
        var state = new DrainState
        {
            Clusters = _clusters.Select(c => new ClusterState
            {
                Id = c.Id,
                Tokens = c.Tokens,
                MatchCount = c.MatchCount,
                LastMatchOrder = c.LastMatchOrder,
            }).ToList(),
            NextClusterId = _nextClusterId,
            MatchOrder = _matchOrder,
        };

        return JsonSerializer.SerializeToUtf8Bytes(state);
    }

    public void RestoreState(byte[] data)
    {
        var state = JsonSerializer.Deserialize<DrainState>(data)
            ?? throw new InvalidOperationException("Failed to deserialize Drain state.");

        _clusters.Clear();
        _root.Children.Clear();
        _root.Clusters.Clear();
        _nextClusterId = state.NextClusterId;
        _matchOrder = state.MatchOrder;

        foreach (var cs in state.Clusters)
        {
            var cluster = new LogCluster
            {
                Id = cs.Id,
                Tokens = cs.Tokens,
                MatchCount = cs.MatchCount,
                LastMatchOrder = cs.LastMatchOrder,
            };

            var tokens = cluster.Tokens.ToArray();
            var leafNode = TraverseTree(tokens);
            leafNode.Clusters.Add(cluster);
            _clusters.Add(cluster);
        }
    }

    private static string[] Tokenize(string message)
    {
        return message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Finds all candidate leaf nodes by searching both exact-token and wildcard branches
    /// at each level. This ensures messages with variable tokens at routing positions
    /// can still match existing clusters.
    /// </summary>
    private List<DrainNode> FindCandidateLeaves(string[] tokens)
    {
        var lengthKey = tokens.Length.ToString();
        if (!_root.Children.TryGetValue(lengthKey, out var lengthNode))
        {
            return [];
        }

        var depth = Math.Min(_config.TreeDepth - 1, tokens.Length);
        List<DrainNode> currentLevel = [lengthNode];

        for (var i = 0; i < depth; i++)
        {
            var token = tokens[i];
            var key = IsVariable(token) ? WildcardToken : token;
            List<DrainNode> nextLevel = [];

            foreach (var node in currentLevel)
            {
                // Try exact token match
                if (node.Children.TryGetValue(key, out var exactChild))
                {
                    nextLevel.Add(exactChild);
                }

                // Also try wildcard branch (if different from exact key)
                if (key != WildcardToken && node.Children.TryGetValue(WildcardToken, out var wildcardChild))
                {
                    nextLevel.Add(wildcardChild);
                }

                // Try all other children as potential matches (different literal tokens)
                foreach (var (childKey, childNode) in node.Children)
                {
                    if (childKey != key && childKey != WildcardToken)
                    {
                        nextLevel.Add(childNode);
                    }
                }
            }

            if (nextLevel.Count == 0)
            {
                return [];
            }

            currentLevel = nextLevel;
        }

        return currentLevel;
    }

    /// <summary>
    /// Traverses the tree using exact token routing, creating nodes as needed.
    /// Used for inserting new clusters.
    /// </summary>
    private DrainNode TraverseTree(string[] tokens)
    {
        var lengthKey = tokens.Length.ToString();

        if (!_root.Children.TryGetValue(lengthKey, out var currentNode))
        {
            currentNode = new DrainNode();
            _root.Children[lengthKey] = currentNode;
        }

        // Traverse depth-1 levels using token values
        var depth = Math.Min(_config.TreeDepth - 1, tokens.Length);
        for (var i = 0; i < depth; i++)
        {
            var token = tokens[i];
            var key = IsVariable(token) ? WildcardToken : token;

            if (!currentNode.Children.TryGetValue(key, out var childNode))
            {
                childNode = new DrainNode();
                currentNode.Children[key] = childNode;
            }

            currentNode = childNode;
        }

        return currentNode;
    }

    private static (LogCluster? Cluster, double Similarity) FindBestMatch(
        DrainNode leafNode, string[] tokens)
    {
        LogCluster? bestCluster = null;
        var bestSimilarity = -1.0;

        foreach (var cluster in leafNode.Clusters)
        {
            var similarity = ComputeSimilarity(cluster.Tokens, tokens);
            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestCluster = cluster;
            }
        }

        return (bestCluster, bestSimilarity);
    }

    private static double ComputeSimilarity(List<string> templateTokens, string[] messageTokens)
    {
        if (templateTokens.Count != messageTokens.Length)
        {
            return 0.0;
        }

        var matchCount = 0;
        for (var i = 0; i < templateTokens.Count; i++)
        {
            if (templateTokens[i] == WildcardToken || templateTokens[i] == messageTokens[i])
            {
                matchCount++;
            }
        }

        return (double)matchCount / templateTokens.Count;
    }

    private static void UpdateTemplate(LogCluster cluster, string[] tokens)
    {
        for (var i = 0; i < cluster.Tokens.Count; i++)
        {
            if (cluster.Tokens[i] != WildcardToken && cluster.Tokens[i] != tokens[i])
            {
                cluster.Tokens[i] = WildcardToken;
            }
        }
    }

    private void EvictLruCluster()
    {
        if (_clusters.Count == 0)
        {
            return;
        }

        var lru = _clusters[0];
        for (var i = 1; i < _clusters.Count; i++)
        {
            if (_clusters[i].LastMatchOrder < lru.LastMatchOrder)
            {
                lru = _clusters[i];
            }
        }

        _clusters.Remove(lru);
        RemoveClusterFromTree(lru);
    }

    private void RemoveClusterFromTree(LogCluster cluster)
    {
        var tokens = cluster.Tokens.ToArray();
        var lengthKey = tokens.Length.ToString();

        if (!_root.Children.TryGetValue(lengthKey, out var currentNode))
        {
            return;
        }

        var depth = Math.Min(_config.TreeDepth - 1, tokens.Length);
        for (var i = 0; i < depth; i++)
        {
            var token = tokens[i];
            var key = IsVariable(token) ? WildcardToken : token;

            if (!currentNode.Children.TryGetValue(key, out var childNode))
            {
                return;
            }

            currentNode = childNode;
        }

        currentNode.Clusters.Remove(cluster);
    }

    private static bool IsVariable(string token)
    {
        if (token == WildcardToken)
        {
            return false;
        }

        // "hasNum" heuristic from Drain paper: any token containing a digit is
        // likely a variable (IP addresses, ports, durations, IDs, etc.)
        if (token.AsSpan().ContainsAny("0123456789"))
        {
            return true;
        }

        // GUIDs (all-hex with dashes, no digits required check already covers most,
        // but keep for the rare all-alpha-hex GUID edge case)
        if (GuidRegex().IsMatch(token))
        {
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex GuidRegex();

    private sealed class DrainState
    {
        public List<ClusterState> Clusters { get; set; } = [];
        public int NextClusterId { get; set; }
        public long MatchOrder { get; set; }
    }

    private sealed class ClusterState
    {
        public int Id { get; set; }
        public List<string> Tokens { get; set; } = [];
        public long MatchCount { get; set; }
        public long LastMatchOrder { get; set; }
    }
}
