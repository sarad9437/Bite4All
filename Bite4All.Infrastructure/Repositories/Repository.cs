using Bite4All.Domain.Common;
using Bite4All.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bite4All.Infrastructure.Repositories;

public class Repository<T>(Bite4AllContext context) : IRepository<T> where T : Entity
{
    public IQueryable<T> Query() => context.Set<T>();

    public Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return context.Set<T>().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await context.Set<T>().AddAsync(entity, cancellationToken);
    }

    public void Update(T entity)
    {
        context.Set<T>().Update(entity);
    }

    public void Delete(T entity)
    {
        context.Set<T>().Remove(entity);
    }
}
