using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
// DI container
builder.Services.Configure<PathMappingOptions>(
    builder.Configuration.GetSection(PathMappingOptions.SectionName));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IStateRepository, SqliteStateRepository>();
builder.Services.AddSingleton<IRuntimePathResolver, RuntimePathResolver>();
builder.Services.AddScoped<IDirectoryAnalyzerService, DirectoryAnalyzerService>();

builder.Services.AddScoped<IValidator<AnalyzeRequestDto>, AnalyzeRequestDtoValidator>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

var connectionString = app.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

var dbPath = new SqliteConnectionStringBuilder(connectionString).DataSource;
if (!Path.IsPathRooted(dbPath))
{
    dbPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), dbPath));
}

var dbDirectory = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDirectory))
{
    Directory.CreateDirectory(dbDirectory);
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseDefaultFiles();
app.UseStaticFiles(); 
app.MapControllers();
app.Run();
