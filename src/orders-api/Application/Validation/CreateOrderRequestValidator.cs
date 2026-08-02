using FluentValidation;
using OrdersApi.Application.DTOs;
using OrdersApi.Application.Interfaces;

namespace OrdersApi.Application.Validation;

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator(IProductCatalogRepository productCatalogRepository)
    {
        RuleFor(x => x.ClienteNombre)
            .NotEmpty().WithMessage("ClienteNombre no puede estar vacío.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("Sku no puede estar vacío.")
            .MustAsync(async (sku, cancellation) =>
                await productCatalogRepository.ExistsBySkuAsync(sku, cancellation))
            .WithMessage("Sku no existe en el catálogo.");

        RuleFor(x => x.Cantidad)
            .InclusiveBetween(1, 100).WithMessage("Cantidad debe estar entre 1 y 100.");
    }
}
