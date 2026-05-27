
namespace DirectoryChangeApp.Validations;

public class AnalyzeRequestDtoValidator: AbstractValidator<AnalyzeRequestDto>
{
    public AnalyzeRequestDtoValidator()
    {
        RuleFor(x => x.DirectoryPath)
            .NotEmpty().WithMessage("Path to catalog can't be empty.")
            .Must(Directory.Exists).WithMessage("Current catalog doesn't exist.");
    }
}