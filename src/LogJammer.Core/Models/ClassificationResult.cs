namespace LogJammer.Core.Models;

public record ClassificationResult(
    Guid? MatchedErrorGroupId,
    double SimilarityScore,
    IReadOnlyList<TagSuggestion> SuggestedTags,
    bool NeedsReview);

public record TagSuggestion(Guid TagId, string TagName, double Confidence);
