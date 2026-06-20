using System.Text;
using DocIntel.Api.Ai;
using DocIntel.Api.Auth;
using DocIntel.Api.Data;
using DocIntel.Api.Ingestion;
using DocIntel.Api.Middleware;
using DocIntel.Api.Repositories;
using DocIntel.Api.Services;
using DocIntel.Api.Validation;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- Options ----
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var aiOptions = builder.Configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();

// ---- Database (in-memory by default so `dotnet run` works with no Postgres) ----
var dbProvider = builder.Configuration.GetValue<string>("Database:Provider") ?? "InMemory";
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (dbProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
    {
        var conn = builder.Configuration.GetConnectionString("Postgres")
                   ?? "Host=localhost;Port=5432;Database=docintel;Username=docintel;Password=docintel";
        options.UseNpgsql(conn);
    }
    else
    {
        options.UseInMemoryDatabase("docintel");
    }
});

// ---- Repositories ----
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IChunkRepository, ChunkRepository>();

// ---- Domain services ----
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();

// ---- AI / RAG clients (interface-based so Stub | AzureOpenAI | OpenAI swap cleanly) ----
if (aiOptions.Provider.Equals("Stub", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IEmbeddingClient>(_ => new StubEmbeddingClient(aiOptions.EmbeddingDimensions));
    builder.Services.AddSingleton<ILlmClient, StubLlmClient>();
}
else
{
    builder.Services.AddHttpClient<IEmbeddingClient, OpenAiEmbeddingClient>();
    builder.Services.AddHttpClient<ILlmClient, OpenAiLlmClient>();
}

// ---- Validation ----
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// ---- Auth ----
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
        };
    });
builder.Services.AddAuthorization();

// ---- CORS for the Angular dev server ----
const string CorsPolicy = "frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins(
            builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
            ?? new[] { "http://localhost:4200" })
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---- Ensure schema exists ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
    }
    else
    {
        db.Database.EnsureCreated();
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed so the WebApplicationFactory in the test project can boot the app.
public partial class Program { }
