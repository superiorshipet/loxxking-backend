using System.Linq.Expressions;

namespace Application.Common.Interfaces;

public interface IRepository<T> where T : class
{
    IQueryable<T> Query(); // للـ reads المعقدة (فلاتر، Include، Select)
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Remove(T entity);
}
