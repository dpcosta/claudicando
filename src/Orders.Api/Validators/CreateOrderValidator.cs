using FluentValidation;
using Orders.Api.DTOs;

namespace Orders.Api.Validators;

public class CreateOrderValidator : AbstractValidator<CreateOrderDto>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId é obrigatório")
            .MaximumLength(100).WithMessage("UserId deve ter no máximo 100 caracteres");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("O pedido deve conter pelo menos um item")
            .Must(items => items != null && items.Count > 0)
            .WithMessage("O pedido deve conter pelo menos um item");

        RuleForEach(x => x.Items).SetValidator(new CreateOrderItemValidator());
    }
}

public class CreateOrderItemValidator : AbstractValidator<CreateOrderItemDto>
{
    public CreateOrderItemValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("ProductId é obrigatório");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantidade deve ser maior que zero")
            .LessThanOrEqualTo(1000).WithMessage("Quantidade não pode exceder 1000 unidades");
    }
}
