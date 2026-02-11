using LogJammer.Core.Entities;
using LogJammer.Core.Models;

namespace LogJammer.Core.Interfaces;

public interface IClassificationService
{
    Task<ClassificationResult> ClassifyAsync(KnownError error, CancellationToken ct = default);
    Task RecalculateTagCentroidAsync(Guid tagId, CancellationToken ct = default);
    Task RecalculateAllCentroidsAsync(CancellationToken ct = default);
}
