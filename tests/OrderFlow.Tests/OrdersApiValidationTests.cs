using FluentAssertions;
using Moq;
using OrdersApi.Application.DTOs;
using OrdersApi.Application.Interfaces;
using OrdersApi.Application.Validation;
using Xunit;

namespace OrderFlow.Tests;

public class OrdersApiValidationTests
{
    private readonly Mock<IProductCatalogRepository> _catalogMock;
    private readonly CreateOrderRequestValidator _validator;

    public OrdersApiValidationTests()
    {
        _catalogMock = new Mock<IProductCatalogRepository>();
        _catalogMock
            .Setup(r => r.ExistsBySkuAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string sku, CancellationToken _) => sku is "ABC-01" or "ABC-02" or "ABC-03");

        _validator = new CreateOrderRequestValidator(_catalogMock.Object);
    }

    [Theory]
    [InlineData("", "ABC-01", 5, "ClienteNombre no puede estar vacío.")]
    [InlineData("Juan Perez", "", 5, "Sku no puede estar vacío.")]
    [InlineData("Juan Perez", "ABC-01", 0, "Cantidad debe estar entre 1 y 100.")]
    [InlineData("Juan Perez", "ABC-01", 101, "Cantidad debe estar entre 1 y 100.")]
    public async Task CreateOrderRequest_WithInvalidData_ShouldFailValidation(
        string clienteNombre,
        string sku,
        int cantidad,
        string expectedErrorMessage)
    {
        var request = new CreateOrderRequest(clienteNombre, sku, cantidad);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedErrorMessage));
    }

    [Fact]
    public async Task CreateOrderRequest_WithUnknownSku_ShouldFailValidation()
    {
        var request = new CreateOrderRequest("Juan Perez", "SKU-INVALIDO", 5);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Sku no existe en el catálogo."));
    }

    [Fact]
    public async Task CreateOrderRequest_WithValidData_ShouldPassValidation()
    {
        var request = new CreateOrderRequest("Carlos Ariza", "ABC-01", 10);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
