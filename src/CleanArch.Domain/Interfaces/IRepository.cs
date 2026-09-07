using CleanArch.Domain.Common;
using System.Linq.Expressions;

namespace CleanArch.Domain.Interfaces;

/// <summary>Generic repository contract. Kept in Domain so Application depends only on abstractions.</summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Remove(T entity);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
