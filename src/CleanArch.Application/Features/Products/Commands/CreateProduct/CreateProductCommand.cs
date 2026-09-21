using CleanArch.Application.DTOs;
using MediatR;

namespace CleanArch.Application.Features.Products.Commands.CreateProduct;

public record CreateProductCommand(
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    string Sku) : IRequest<ProductDto>;
