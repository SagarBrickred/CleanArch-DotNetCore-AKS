using CleanArch.Application.DTOs;
using MediatR;

namespace CleanArch.Application.Features.Products.Queries.GetProductById;

public record GetProductByIdQuery(int Id) : IRequest<ProductDto>;
