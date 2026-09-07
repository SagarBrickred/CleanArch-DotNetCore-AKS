using CleanArch.Domain.Entities;
using CleanArch.Domain.Interfaces;

namespace CleanArch.Application.Interfaces;

/// <summary>Product-specific repository extension point beyond the generic CRUD contract.</summary>
public interface IProductRepository : IRepository<Product>
{
    Task<bool> SkuExistsAsync(string sku, int? excludingId = null, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? search, CancellationToken cancellationToken = default);
}
