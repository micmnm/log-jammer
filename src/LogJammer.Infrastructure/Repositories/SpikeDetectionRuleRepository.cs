using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Infrastructure.Repositories;

public class SpikeDetectionRuleRepository(LogJammerDbContext context) : ISpikeDetectionRuleRepository
{
    public async Task<SpikeDetectionRule?> GetByKnownErrorIdAsync(Guid? knownErrorId, CancellationToken cancellationToken = default)
    {
        // Try specific rule first, then fall back to global default
        if (knownErrorId.HasValue)
        {
            var specific = await context.SpikeDetectionRules
                .FirstOrDefaultAsync(r => r.KnownErrorId == knownErrorId.Value, cancellationToken);
            if (specific is not null) return specific;
        }

        return await GetGlobalDefaultAsync(cancellationToken);
    }

    public async Task<SpikeDetectionRule?> GetGlobalDefaultAsync(CancellationToken cancellationToken = default)
    {
        return await context.SpikeDetectionRules
            .FirstOrDefaultAsync(r => r.KnownErrorId == null, cancellationToken);
    }

    public async Task<SpikeDetectionRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.SpikeDetectionRules
            .Include(r => r.KnownError)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SpikeDetectionRule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.SpikeDetectionRules
            .Include(r => r.KnownError)
            .OrderBy(r => r.KnownErrorId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<SpikeDetectionRule> AddAsync(SpikeDetectionRule rule, CancellationToken cancellationToken = default)
    {
        context.SpikeDetectionRules.Add(rule);
        await context.SaveChangesAsync(cancellationToken);
        return rule;
    }

    public async Task UpdateAsync(SpikeDetectionRule rule, CancellationToken cancellationToken = default)
    {
        context.SpikeDetectionRules.Update(rule);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.SpikeDetectionRules
            .Where(r => r.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
