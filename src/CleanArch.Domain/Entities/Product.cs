using CleanArch.Domain.Common;
using CleanArch.Domain.Exceptions;

namespace CleanArch.Domain.Entities;

/// <summary>
/// Sample aggregate root for the CRUD reference implementation.
/// Replace with your real domain entity — the surrounding layers (repository,
/// CQRS handlers, controller, EF configuration) follow the same pattern for any entity.
/// </summary>
public class Product : BaseEntity
{
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public int StockQuantity { get; private set; }
    public string Sku { get; private set; } = default!;

    private Product() { } // EF Core

    public Product(string name, string? description, decimal price, int stockQuantity, string sku)
    {
        SetName(name);
        Description = description;
        SetPrice(price);
        SetStock(stockQuantity);
        Sku = string.IsNullOrWhiteSpace(sku) ? throw new DomainValidationException("SKU is required.") : sku.Trim().ToUpperInvariant();
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            throw new DomainValidationException("Product name must be 1-200 characters.");
        Name = name.Trim();
    }

    public void SetPrice(decimal price)
    {
        if (price < 0) throw new DomainValidationException("Price cannot be negative.");
        Price = price;
    }

    public void SetStock(int quantity)
    {
        if (quantity < 0) throw new DomainValidationException("Stock quantity cannot be negative.");
        StockQuantity = quantity;
    }

    public void UpdateDetails(string name, string? description, decimal price, int stockQuantity)
    {
        SetName(name);
        Description = description;
        SetPrice(price);
        SetStock(stockQuantity);
        LastModifiedAtUtc = DateTime.UtcNow;
    }

    public void MarkDeleted()
    {
        IsDeleted = true;
        LastModifiedAtUtc = DateTime.UtcNow;
    }
}
