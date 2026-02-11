using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Infrastructure.Repositories;

public class UserOverrideRepository(LogJammerDbContext context) : IUserOverrideRepository
{
    public async Task<UserOverride> AddAsync(UserOverride userOverride, CancellationToken cancellationToken = default)
    {
        context.UserOverrides.Add(userOverride);
        await context.SaveChangesAsync(cancellationToken);
        return userOverride;
    }

    public async Task<IReadOnlyList<UserOverride>> GetByKnownErrorAsync(Guid knownErrorId, CancellationToken cancellationToken = default)
    {
        return await context.UserOverrides
            .Where(o => o.KnownErrorId == knownErrorId)
            .OrderByDescending(o => o.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<UserOverride?> GetByKnownErrorAndTypeAsync(Guid knownErrorId, string overrideType, CancellationToken cancellationToken = default)
    {
        return await context.UserOverrides
            .Where(o => o.KnownErrorId == knownErrorId && o.OverrideType == overrideType)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
