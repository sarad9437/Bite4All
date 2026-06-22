using Bite4All.Domain.Common;

namespace Bite4All.Domain.Repositories;

public interface IRepository<T> where T : Entity
{
    IQueryable<T> Query();
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Delete(T entity);
}
