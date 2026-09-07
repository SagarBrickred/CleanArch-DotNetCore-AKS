using CleanArch.Application.Features.Products.Commands.CreateProduct;
using FluentAssertions;
using Xunit;

namespace CleanArch.Application.UnitTests.Products;

public class CreateProductCommandValidatorTests
{
    private readonly CreateProductCommandValidator _validator = new();

    [Fact]
    public void Validate_NegativePrice_FailsValidation()
    {
        var command = new CreateProductCommand("Widget", null, -1m, 10, "WID-001");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Price");
    }

    [Fact]
    public void Validate_InvalidSkuCharacters_FailsValidation()
    {
        var command = new CreateProductCommand("Widget", null, 5m, 10, "WID 001!");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Sku");
    }

    [Fact]
    public void Validate_ValidCommand_PassesValidation()
    {
        var command = new CreateProductCommand("Widget", "desc", 5m, 10, "WID-001");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }
}
