using AutoMapper;
using CleanArch.Application.Common.Exceptions;
using CleanArch.Application.DTOs;
using CleanArch.Application.Interfaces;
using CleanArch.Domain.Entities;
using MediatR;

namespace CleanArch.Application.Features.Products.Commands.CreateProduct;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, ProductDto>
{
    private readonly IProductRepository _repository;
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public CreateProductCommandHandler(IProductRepository repository, IApplicationDbContext context, IMapper mapper)
    {
        _repository = repository;
        _context = context;
        _mapper = mapper;
    }

    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (await _repository.SkuExistsAsync(request.Sku, cancellationToken: cancellationToken))
            throw new ConflictException($"A product with SKU '{request.Sku}' already exists.");

        var product = new Product(request.Name, request.Description, request.Price, request.StockQuantity, request.Sku);

        await _repository.AddAsync(product, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ProductDto>(product);
    }
}
