namespace DirectoryChangeApp.Validations;

public class AnalyzeRequestDtoValidator : AbstractValidator<AnalyzeRequestDto>
{
    public AnalyzeRequestDtoValidator(IRuntimePathResolver runtimePathResolver)
    {
        RuleFor(x => x.DirectoryPath)
            .NotEmpty().WithMessage("Path to catalog can't be empty.")
            .Must(path =>
            {
                var runtimePath = runtimePathResolver.Resolve(path);
                return !string.IsNullOrWhiteSpace(runtimePath) && Directory.Exists(runtimePath);
            })
            .WithMessage("Current catalog doesn't exist or is not mounted in current runtime.");
    }
}