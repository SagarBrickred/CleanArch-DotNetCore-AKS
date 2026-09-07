using AutoMapper;
using CleanArch.Application.Common.Exceptions;
using CleanArch.Application.Common.Mappings;
using CleanArch.Application.Features.Products.Commands.CreateProduct;
using CleanArch.Application.Interfaces;
using CleanArch.Domain.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace CleanArch.Application.UnitTests.Products;

public class CreateProductCommandHandlerTests
{
    private readonly Mock<IProductRepository> _repositoryMock = new();
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly IMapper _mapper;

    public CreateProductCommandHandlerTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory());
        _mapper = config.CreateMapper();
    }

    private static Microsoft.Extensions.Logging.ILoggerFactory NullLoggerFactory() =>
        Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;

    [Fact]
    public async Task Handle_ValidRequest_CreatesProductAndReturnsDto()
    {
        // Arrange
        _repositoryMock.Setup(r => r.SkuExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product p, CancellationToken _) => p);
        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateProductCommandHandler(_repositoryMock.Object, _contextMock.Object, _mapper);
        var command = new CreateProductCommand("Widget", "A basic widget", 9.99m, 100, "WID-001");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Widget");
        result.Sku.Should().Be("WID-001");
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateSku_ThrowsConflictException()
    {
        // Arrange
        _repositoryMock.Setup(r => r.SkuExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new CreateProductCommandHandler(_repositoryMock.Object, _contextMock.Object, _mapper);
        var command = new CreateProductCommand("Widget", null, 9.99m, 100, "DUP-001");

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
