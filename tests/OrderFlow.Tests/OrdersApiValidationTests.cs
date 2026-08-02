using FluentAssertions;
using OrdersApi.Application.DTOs;
using OrdersApi.Application.Validation;
using Xunit;

namespace OrderFlow.Tests;

public class OrdersApiValidationTests
{
    private readonly CreateOrderRequestValidator _validator;

    public OrdersApiValidationTests()
    {
        _validator = new CreateOrderRequestValidator();
    }

    [Theory]
    [InlineData("", "ABC-01", 5, "ClienteNombre no puede estar vacío.")]
    [InlineData("Juan Perez", "", 5, "Sku no puede estar vacío.")]
    [InlineData("Juan Perez", "ABC-01", 0, "Cantidad debe estar entre 1 y 100.")]
    [InlineData("Juan Perez", "ABC-01", 101, "Cantidad debe estar entre 1 y 100.")]
    public void CreateOrderRequest_WithInvalidData_ShouldFailValidation(
        string clienteNombre, 
        string sku, 
        int cantidad, 
        string expectedErrorMessage)
    {
        // Arrange
        var request = new CreateOrderRequest(clienteNombre, sku, cantidad);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedErrorMessage));
    }

    [Fact]
    public void CreateOrderRequest_WithValidData_ShouldPassValidation()
    {
        // Arrange
        var request = new CreateOrderRequest("Carlos Ariza", "ABC-01", 10);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
