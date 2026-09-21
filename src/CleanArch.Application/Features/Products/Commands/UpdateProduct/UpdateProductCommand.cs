using MediatR;

namespace CleanArch.Application.Features.Products.Commands.UpdateProduct;

public record UpdateProductCommand(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    byte[] RowVersion) : IRequest<Unit>;
