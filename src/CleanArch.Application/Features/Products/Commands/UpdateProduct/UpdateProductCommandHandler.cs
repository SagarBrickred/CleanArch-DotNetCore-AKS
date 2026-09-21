using CleanArch.Application.Common.Exceptions;
using CleanArch.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CleanArch.Application.Features.Products.Commands.UpdateProduct;

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Unit>
{
    private readonly IProductRepository _repository;
    private readonly IApplicationDbContext _context;

    public UpdateProductCommandHandler(IProductRepository repository, IApplicationDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    public async Task<Unit> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Product", request.Id);

        // Optimistic concurrency: caller must present the RowVersion they last read.
        product.RowVersion = request.RowVersion;
        product.UpdateDetails(request.Name, request.Description, request.Price, request.StockQuantity);

        _repository.Update(product);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                $"Product {request.Id} was modified by another process. Reload and retry.");
        }

        return Unit.Value;
    }
}
