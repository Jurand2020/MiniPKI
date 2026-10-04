using MiniPKI.Api.Auth;
using MiniPKI.Api.Middleware;
using MiniPKI.Core.Configuration;
using MiniPKI.Core.Services;
using MiniPKI.Infrastructure.Services;
using MiniPKI.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// Configure data path (read from config so tests can override)
var dataPath = builder.Configuration.GetValue<string>("Storage:DataPath") ?? "/data";
if (!Path.IsPathRooted(dataPath))
    dataPath = Path.GetFullPath(dataPath);

// Serialize enums as strings in JSON
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Register path provider as factory (reads config at resolution time)
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var path = config.GetValue<string>("Storage:DataPath") ?? "/data";
    if (!Path.IsPathRooted(path))
        path = Path.GetFullPath(path);
    var provider = new DataPathProvider(path);
    provider.EnsureDirectoriesExist();
    return provider;
});

// Register configuration
var pkiConfig = new PkiConfiguration();
builder.Services.AddSingleton(pkiConfig);

// Register configuration store
builder.Services.AddSingleton<FileConfigurationStore>();

// Register certificate store
builder.Services.AddSingleton<FileCertificateStore>();

// Register services
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<FileAuditService>();
builder.Services.AddSingleton<IAuditService>(sp => sp.GetRequiredService<FileAuditService>());
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<BouncyCastleCertificateAuthorityService>();
builder.Services.AddSingleton<BouncyCastleCertificateService>();
builder.Services.AddSingleton<BouncyCastleCrlService>();

// Initialize CA on startup
builder.Services.AddHostedService<CaInitializerService>();

// Add controllers
// (already registered above with JSON options)

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<AuthenticationMiddleware>();

app.UseHttpsRedirection();
app.MapControllers();

// Health endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

public partial class Program;
