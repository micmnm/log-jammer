using LogJammer.Core.Entities;
using LogJammer.Core.Models;

namespace LogJammer.Core.Interfaces;

public interface IFingerprintCalculator
{
    string ComputeFingerprint(MappedLogEntry entry, IReadOnlyList<FingerprintConfig> configs);
}
