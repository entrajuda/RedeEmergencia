using FluentValidation;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace REA.Emergencia.Web.Models;

public sealed class PedidoBemInputModelValidator : AbstractValidator<PedidoBemInputModel>
{
    private static readonly Regex DigitsOnlyRegex = new(@"^\d+$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(
        @"^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]{1,64}@[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+$",
        RegexOptions.Compiled);
    private static readonly Regex PostalCodeRegex = new(@"^\d{4}-\d{3}$", RegexOptions.Compiled);

    public PedidoBemInputModelValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .WithMessage("O nome é obrigatório.")
            .MaximumLength(100)
            .WithMessage("O nome não pode exceder 100 caracteres.");

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
            .Must(IsValidEmailStrict)
            .WithMessage("Introduza um endereço de email válido.")
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
            .MaximumLength(100);

        RuleFor(x => x.Concelho)
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

        RuleFor(x => x.NeededProductTypes)
            .NotEmpty()
            .WithMessage("Selecione pelo menos um tipo de produtos.")
            .Must(OnlyContainValidProductTypes)
            .WithMessage("Existe um tipo de produtos inválido.");

        RuleFor(x => x.OtherNeededProductTypesDetails)
            .NotEmpty()
            .WithMessage("Especifique os outros produtos.")
            .MaximumLength(300)
            .When(x => x.NeededProductTypes.Contains("Outros"));

        RuleFor(x => x.OtherNeededProductTypesDetails)
            .MaximumLength(300)
            .When(x => !string.IsNullOrWhiteSpace(x.OtherNeededProductTypesDetails));

        RuleFor(x => x.Suggestions)
            .MaximumLength(1000)
            .WithMessage("As sugestões não podem exceder 1000 caracteres.");

        RuleFor(x => x.HouseholdSize)
            .Must((model, householdSize) => HaveConsistentHouseholdBreakdown(model, householdSize))
            .WithMessage("A soma de crianças (<12), jovens (13-17), adultos (18+) e pessoas com mais de 65 anos deve ser igual ao total de pessoas no agregado.");
    }

    private static bool IsValidPhone(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value
            .Trim()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty)
            .Replace(".", string.Empty);

        if (normalized.StartsWith("00"))
        {
            normalized = $"+{normalized[2..]}";
        }

        string numberPart;
        if (normalized.StartsWith("+351"))
        {
            numberPart = normalized[4..];
        }
        else if (normalized.StartsWith("351") && normalized.Length == 12)
        {
            numberPart = normalized[3..];
        }
        else
        {
            numberPart = normalized;
        }

        return numberPart.Length == 9
            && DigitsOnlyRegex.IsMatch(numberPart)
            && (numberPart.StartsWith("2") || numberPart.StartsWith("9"));
    }

    private static bool IsValidPostalCode(string value)
    {
        var normalized = value.Replace(" ", string.Empty);
        return PostalCodeRegex.IsMatch(normalized);
    }

    private static bool IsValidEmailStrict(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.Contains(" "))
        {
            return false;
        }

        if (!EmailRegex.IsMatch(normalized))
        {
            return false;
        }

        try
        {
            var mailAddress = new MailAddress(normalized);
            if (!string.Equals(mailAddress.Address, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var atIndex = normalized.LastIndexOf('@');
            if (atIndex <= 0 || atIndex >= normalized.Length - 1)
            {
                return false;
            }

            var domain = normalized[(atIndex + 1)..];
            if (!domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.') || domain.Contains(".."))
            {
                return false;
            }

            var tld = domain[(domain.LastIndexOf('.') + 1)..];
            if (tld.Length is < 2 or > 6)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool OnlyContainValidProductTypes(List<string> values)
    {
        return values.All(PedidoBemInputModel.AvailableProductTypes.Contains);
    }

    private static bool HaveConsistentHouseholdBreakdown(PedidoBemInputModel model, int householdSize)
    {
        var totalByAges =
            model.ChildrenUnder12 +
            model.Youth13To17 +
            model.Adults18Plus +
            model.Seniors65Plus;

        return householdSize == totalByAges;
    }
}
