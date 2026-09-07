using CleanArch.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CleanArch.Application.Interfaces;

/// <summary>
/// Abstraction over the EF Core DbContext so Application never references Infrastructure/EF directly.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Product> Products { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
