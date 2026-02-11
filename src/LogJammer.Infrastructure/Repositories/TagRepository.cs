using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Infrastructure.Repositories;

public class TagRepository(LogJammerDbContext context) : ITagRepository
{
    public async Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Tags
            .OrderBy(t => t.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Tag?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Tags.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Tag?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await context.Tags.FirstOrDefaultAsync(t => t.Name == name, cancellationToken);
    }

    public async Task<Tag> AddAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        context.Tags.Add(tag);
        await context.SaveChangesAsync(cancellationToken);
        return tag;
    }

    public async Task UpdateAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        context.Tags.Update(tag);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        context.Tags.Remove(tag);
        await context.SaveChangesAsync(cancellationToken);
    }
}
