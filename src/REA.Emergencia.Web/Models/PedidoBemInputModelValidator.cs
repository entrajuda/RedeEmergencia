using FluentValidation;
using System.Text.RegularExpressions;

namespace REA.Emergencia.Web.Models;

public sealed class PedidoBemInputModelValidator : AbstractValidator<PedidoBemInputModel>
{
    private static readonly Regex PhoneRegex = new(@"^(?:\+351\s?)?(?:2\d{8}|9\d{8})$", RegexOptions.Compiled);
    private static readonly Regex PostalCodeRegex = new(@"^\d{4}-\d{3}$", RegexOptions.Compiled);

    public PedidoBemInputModelValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .WithMessage("O nome é obrigatório.")
            .MaximumLength(200);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .WithMessage("O número de telemóvel é obrigatório.")
            .Must(IsValidPhone)
            .WithMessage("Introduza um número de telemóvel válido.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("O email é obrigatório.")
            .EmailAddress()
            .WithMessage("Introduza um email válido.")
            .MaximumLength(200);

        RuleFor(x => x.Address)
            .NotEmpty()
            .WithMessage("A morada é obrigatória.")
            .MaximumLength(300);

        RuleFor(x => x.PostalCode)
            .NotEmpty()
            .WithMessage("O código postal é obrigatório.")
            .Must(IsValidPostalCode)
            .WithMessage("Introduza um código postal no formato 0000-000.");

        RuleFor(x => x.Localidade)
            .NotEmpty()
            .WithMessage("A localidade é obrigatória.")
            .MaximumLength(100);

        RuleFor(x => x.Freguesia)
            .NotEmpty()
            .WithMessage("A freguesia é obrigatória.")
            .MaximumLength(100);

        RuleFor(x => x.Concelho)
            .NotEmpty()
            .WithMessage("O concelho é obrigatório.")
            .MaximumLength(100);

        RuleFor(x => x.IdentificationNumber)
            .NotEmpty()
            .WithMessage("O número de identificação é obrigatório.")
            .MaximumLength(100);

        RuleFor(x => x.Age)
            .InclusiveBetween(0, 120)
            .WithMessage("A idade deve estar entre 0 e 120.");

        RuleFor(x => x.HouseholdSize)
            .InclusiveBetween(1, 50)
            .WithMessage("Indique um número de pessoas válido.");

        RuleFor(x => x.ChildrenUnder12)
            .InclusiveBetween(0, 50)
            .WithMessage("Indique um número de crianças válido.");

        RuleFor(x => x.Youth13To17)
            .InclusiveBetween(0, 50)
            .WithMessage("Indique um número de jovens válido.");

        RuleFor(x => x.Adults18Plus)
            .InclusiveBetween(0, 50)
            .WithMessage("Indique um número de pessoas adultas válido.");

        RuleFor(x => x.Seniors65Plus)
            .InclusiveBetween(0, 50)
            .WithMessage("Indique um número de pessoas com mais de 65 anos válido.");

        RuleFor(x => x.ReceivesFoodSupport)
            .NotNull()
            .WithMessage("Selecione uma opção.");

        RuleFor(x => x.FoodSupportInstitutionName)
            .NotEmpty()
            .WithMessage("Indique o nome da instituição.")
            .MaximumLength(200)
            .When(x => x.ReceivesFoodSupport == true);

        RuleFor(x => x.CanPickUpNearby)
            .NotNull()
            .WithMessage("Selecione uma opção.");

        RuleFor(x => x.Suggestions)
            .MaximumLength(1000)
            .WithMessage("As sugestões não podem exceder 1000 caracteres.");
    }

    private static bool IsValidPhone(string value)
    {
        var normalized = value.Replace(" ", string.Empty).Replace("-", string.Empty);
        return PhoneRegex.IsMatch(normalized);
    }

    private static bool IsValidPostalCode(string value)
    {
        var normalized = value.Replace(" ", string.Empty);
        return PostalCodeRegex.IsMatch(normalized);
    }
}
