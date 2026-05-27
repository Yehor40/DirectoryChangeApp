
namespace DirectoryChangeApp.Validations;

public class AnalyzeRequestDtoValidator: AbstractValidator<AnalyzeRequestDto>
{
    public AnalyzeRequestDtoValidator()
    {
        RuleFor(x => x.DirectoryPath)
            .NotEmpty().WithMessage("Cesta k adresáři nesmí být prázdná.")
            .Must(Directory.Exists).WithMessage("Zadaný adresář neexistuje na serveru.");
    }
}