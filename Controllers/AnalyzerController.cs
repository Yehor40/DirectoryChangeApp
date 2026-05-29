
namespace DirectoryChangeApp.Controllers;

[ApiController]
[Route("api/analyze")]
public class AnalyzerController(IDirectoryAnalyzerService analyzerService,IValidator<AnalyzeRequestDto> _validator, ILogger<AnalyzerController> _logger): ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> AnalyzeDirectory([FromBody] AnalyzeRequestDto requestDto)
    {
        var domainResult = await analyzerService.AnalyzeAsync(requestDto.DirectoryPath);
        
        return Ok(domainResult.ToResponseDto());
    }
}