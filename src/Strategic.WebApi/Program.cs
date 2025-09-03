using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Impostor.Plugins.SemanticAnnotator.Infrastructure;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using Strategic.WebApi.Authorization;
using Strategic.WebApi.Infrastructure;
using Strategic.WebApi.Ports;
using Strategic.WebApi.Security;
using Strategic.WebApi.Seeders;

const int MinKeyBytes = 32;

static byte[] ResolveSigningKey(string? raw, bool devGenerateIfMissing)
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        if (devGenerateIfMissing)
        {
            var buf = new byte[MinKeyBytes];
            RandomNumberGenerator.Fill(buf);
            Console.WriteLine("[JWT] No SigningKey found. Generated ephemeral dev key.");
            return buf;
        }
        throw new InvalidOperationException("Jwt:SigningKey is required.");
    }

    raw = raw.Trim();

    // base64:
    if (raw.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
    {
        var b64 = raw.Substring("base64:".Length);
        try
        {
            var bytes = Convert.FromBase64String(b64);
            if (bytes.Length < MinKeyBytes)
                throw new InvalidOperationException($"Jwt:SigningKey (base64) must be at least {MinKeyBytes} bytes.");
            return bytes;
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Jwt:SigningKey base64 is invalid.");
        }
    }

    // base64url:
    if (raw.StartsWith("base64url:", StringComparison.OrdinalIgnoreCase))
    {
        var b64u = raw.Substring("base64url:".Length)
                      .Replace('-', '+').Replace('_', '/');
        // padding
        switch (b64u.Length % 4)
        {
            case 2: b64u += "=="; break;
            case 3: b64u += "="; break;
        }
        try
        {
            var bytes = Convert.FromBase64String(b64u);
            if (bytes.Length < MinKeyBytes)
                throw new InvalidOperationException($"Jwt:SigningKey (base64url) must be at least {MinKeyBytes} bytes.");
            return bytes;
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Jwt:SigningKey base64url is invalid.");
        }
    }

    // plain text → UTF8 bytes
    var plain = Encoding.UTF8.GetBytes(raw);
    if (plain.Length < MinKeyBytes)
        throw new InvalidOperationException($"Jwt:SigningKey must be at least {MinKeyBytes} bytes (got {plain.Length}).");
    return plain;
}

var builder = WebApplication.CreateBuilder(args);

// ===== Options =====
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

// ===== CORS (limita ai tuoi front-end) =====
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
        p.WithOrigins(allowedOrigins)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials());
});

// ===== Controllers & Swagger =====
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SemanticAnnotator API", Version = "v1" });
    c.EnableAnnotations();

    // Definizione dello schema Bearer (solo definizione, NESSUN requisito globale qui!)
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Inserisci solo il JWT (Swagger aggiunge 'Bearer ' automaticamente).",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    });

    // Requisito applicato per-endpoint SOLO se [Authorize] e non [AllowAnonymous]
    c.OperationFilter<SwaggerAuthOperationFilter>();
});




// ===== Redis =====
var redisConn = builder.Configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));

// ===== Stores =====
// (Event storage già tuo)
builder.Services.AddSingleton<IGameEventStorage, RedisGameEventStorage>();

// Auth/ACL/Evals (le implementazioni Redis che ti ho dato prima)
builder.Services.AddSingleton<IAuthStore, RedisAuthStore>();
builder.Services.AddSingleton<IAccessControlStore, RedisAccessControlStore>();
builder.Services.AddSingleton<IEvaluationStore, RedisEvaluationStore>();

// Refresh token store su Redis
builder.Services.AddSingleton<ITokenStore, RedisTokenStore>();

// ===== JWT Auth =====

// 1) Bind delle opzioni
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwtOpts = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Missing Jwt section.");

if (string.IsNullOrWhiteSpace(jwtOpts.SigningKey) || jwtOpts.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 chars.");

// 2) Chiave simmetrica condivisa (UGUALE per issuer e bearer)
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpts.SigningKey));

// 3) AuthN + JWT Bearer
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,

            ValidateIssuer = true,
            ValidIssuer = jwtOpts.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtOpts.Audience,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5)
        };

        // Log utili in debug
        o.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine($"[JWT] Auth failed: {ctx.Exception.GetType().Name} - {ctx.Exception.Message}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// 4) Issuer (usa le stesse opzioni/chiave tramite IOptions<JwtOptions>)
builder.Services.AddSingleton<JwtIssuer>();


var app = builder.Build();

// ===== Pipeline =====
if (app.Environment.IsDevelopment() /* || true per sempre */)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.RoutePrefix = "swagger";
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SemanticAnnotator API v1");
    });
}

app.UseCors();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
var seedUser = app.Configuration["SeedAdmin:UserId"] ?? "admin";
var seedKey = app.Configuration["SeedAdmin:SessionKey"] ?? "admin-key-123";
await AdminUserSeeder.SeedAsync(app, seedUser, seedKey);

app.Run();

// ===== Options classes =====
public sealed class JwtOptions
{
    public string Issuer { get; set; } = "SemanticAnnotator";
    public string Audience { get; set; } = "SemanticAnnotatorClient";
    public string SigningKey { get; set; } = default!;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
    public string RefreshCookieName { get; set; } = "rt";
}
