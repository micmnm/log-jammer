using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Infrastructure.Repositories;

public class ClassificationConfigRepository(LogJammerDbContext context) : IClassificationConfigRepository
{
    public async Task<ClassificationConfig?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        return await context.ClassificationConfigs
            .FirstOrDefaultAsync(c => c.Key == key, cancellationToken);
    }

    public async Task<IReadOnlyList<ClassificationConfig>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.ClassificationConfigs
            .OrderBy(c => c.Key)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<ClassificationConfig> UpsertAsync(string key, string value, string? description = null, CancellationToken cancellationToken = default)
    {
        var existing = await context.ClassificationConfigs
            .FirstOrDefaultAsync(c => c.Key == key, cancellationToken);

        if (existing is not null)
        {
            existing.Value = value;
            if (description is not null)
                existing.Description = description;
            context.ClassificationConfigs.Update(existing);
        }
        else
        {
            existing = new ClassificationConfig
            {
                Key = key,
                Value = value,
                Description = description
            };
            context.ClassificationConfigs.Add(existing);
        }

        await context.SaveChangesAsync(cancellationToken);
        return existing;
    }
}
