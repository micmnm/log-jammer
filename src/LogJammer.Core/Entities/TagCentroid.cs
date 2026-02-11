using Pgvector;

namespace LogJammer.Core.Entities;

public class TagCentroid
{
    public Guid Id { get; set; }
    public Guid TagId { get; set; }
    public Vector? CentroidVector { get; set; }
    public int ErrorCount { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Tag Tag { get; set; } = null!;
}
