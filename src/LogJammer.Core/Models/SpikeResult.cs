using LogJammer.Core.Enums;

namespace LogJammer.Core.Models;

public record SpikeResult(
    Guid KnownErrorId,
    ThresholdType ThresholdType,
    double ThresholdValue,
    double ActualValue,
    bool IsSpike);
