
namespace DirectoryChangeApp.Controllers;

[ApiController]
[Route("api/analyze")]
public class AnalyzerController(IDirectoryAnalyzerService analyzerService,IValidator<AnalyzeRequestDto> _validator, ILogger<AnalyzerController> _logger): ControllerBase
{
    [HttpPost]
    public IActionResult AnalyzeDirectory([FromBody] AnalyzeRequestDto requestDto)
    {
        _logger.LogInformation("Received HTTP request for catalog analysis.");
     
        var validationResult = _validator.Validate(requestDto);
        
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Request validation failed. Number of errors: {Count}", validationResult.Errors.Count);
            
            var errors = validationResult.Errors.Select(e => e.ErrorMessage);
            return BadRequest(new { Errors = errors });
        }
        var domainResult = analyzerService.Analyze(requestDto.DirectoryPath);
        
        return Ok(domainResult.ToResponseDto());
       
    }
}