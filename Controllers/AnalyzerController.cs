namespace DirectoryChangeApp.Controllers;

[ApiController]
[Route("api/analyze")]
public class AnalyzerController(
    IDirectoryAnalyzerService analyzerService,
    IValidator<AnalyzeRequestDto> validator,
    ILogger<AnalyzerController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> AnalyzeDirectory([FromBody] AnalyzeRequestDto requestDto)
    {
        logger.LogInformation("Received HTTP request for catalog analysis: {Path}", requestDto.DirectoryPath);

        var validationResult = await validator.ValidateAsync(requestDto);
        
        if (!validationResult.IsValid)
        {
            logger.LogWarning("Request validation failed. Number of errors: {Count}", validationResult.Errors.Count);
            var errors = validationResult.Errors.Select(e => e.ErrorMessage);
            return BadRequest(new { Errors = errors });
        }

        try
        {
            var domainResult = await analyzerService.AnalyzeAsync(requestDto.DirectoryPath);
            return Ok(domainResult.ToResponseDto());
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Bad request argument: {Message}", ex.Message);
            return BadRequest(new { Errors = new[] { ex.Message } });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error analyzing directory: {Message}", ex.Message);
            return StatusCode(500, new { Errors = new[] { "An unexpected error occurred on the server." } });
        }
    }
}