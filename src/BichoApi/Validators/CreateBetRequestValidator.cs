using FluentValidation;

public class CreateBetRequestValidator : AbstractValidator<CreateBetRequest>
{
    public CreateBetRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.StakeCents).GreaterThan(0);
        RuleFor(x => x.Positions).NotNull();
        RuleFor(x => x.Payload).NotNull();

        // Validações básicas por modalidade (exemplos)
        When(x => x.Modality == Modality.MILHAR, () =>
        {
            RuleFor(x => x.Payload).Must(p =>
                p.TryGetValue("numbers", out var el) &&
                el is IEnumerable<object> arr &&
                arr.Cast<object>().All(v => v?.ToString()!.Length == 4 && v.ToString()!.All(char.IsDigit))
            ).WithMessage("MILHAR requer payload: { numbers: [ '0000', ... ] }");
        });

        When(x => x.Modality == Modality.CENTENA_3X || x.Modality == Modality.CENTENA, () =>
        {
            RuleFor(x => x.Payload).Must(p =>
                p.ContainsKey("hundred") || p.ContainsKey("hundreds")
            ).WithMessage("CENTENA requer { hundred: '123' } ou { hundreds: ['123', ...] }");
        });
    }
}