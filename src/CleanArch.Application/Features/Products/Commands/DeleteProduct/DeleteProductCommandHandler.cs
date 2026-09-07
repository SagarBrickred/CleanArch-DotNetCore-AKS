using CleanArch.Application.Common.Exceptions;
using CleanArch.Application.Interfaces;
using MediatR;

namespace CleanArch.Application.Features.Products.Commands.DeleteProduct;

public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Unit>
{
    private readonly IProductRepository _repository;
    private readonly IApplicationDbContext _context;

    public DeleteProductCommandHandler(IProductRepository repository, IApplicationDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    public async Task<Unit> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Product", request.Id);

        // Soft delete only — hard deletes are never issued from the API in production.
        product.MarkDeleted();
        _repository.Update(product);
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
