using CleanArch.Application.DTOs;
using MediatR;

namespace CleanArch.Application.Features.Products.Queries.GetProducts;

public record GetProductsQuery(int PageNumber = 1, int PageSize = 20, string? Search = null)
    : IRequest<PaginatedResult<ProductDto>>;
