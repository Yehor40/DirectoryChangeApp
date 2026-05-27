
var builder = WebApplication.CreateBuilder(args);
// DI container
builder.Services.AddScoped<IStateRepository, JsonStateRepository>();
builder.Services.AddScoped<IDirectoryAnalyzerService, DirectoryAnalyzerService>();

builder.Services.AddScoped<IValidator<AnalyzeRequestDto>, AnalyzeRequestDtoValidator>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseDefaultFiles();
app.UseStaticFiles(); 
app.MapControllers();
app.Run();
