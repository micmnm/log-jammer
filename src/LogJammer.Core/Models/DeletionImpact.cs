namespace LogJammer.Core.Models;

public record DeletionImpact(
    int ErrorGroupCount,
    int OccurrenceCount,
    int AlertCount,
    int ClassificationQueueCount,
    int TagCount,
    int RuleCount);
