using System.Security.Cryptography;
using System.Text;
using Impostor.Plugins.SemanticAnnotator.Infrastructure;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using Strategic.WebApi.Authorization;
using Strategic.WebApi.Infrastructure;
using Strategic.WebApi.Ports;
using Strategic.WebApi.Security;
using Strategic.WebApi.Seeders;

const int MinKeyBytes = 32;

var builder = WebApplication.CreateBuilder(args);

// ===== Options =====
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var pathBase = builder.Configuration["AppSettings:PathBase"] ?? "";

// ===== CORS =====
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

    // Bearer schema
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

    c.OperationFilter<SwaggerAuthOperationFilter>();

    // Aggiungi il server base preso da config
    if (!string.IsNullOrEmpty(pathBase))
    {
        c.AddServer(new OpenApiServer { Url = pathBase });
    }
});

// ===== Redis =====
var redisConn = builder.Configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));

// ===== Stores =====
builder.Services.AddSingleton<IGameEventStorage, RedisGameEventStorage>();
builder.Services.AddSingleton<IAuthStore, RedisAuthStore>();
builder.Services.AddSingleton<IAccessControlStore, RedisAccessControlStore>();
builder.Services.AddSingleton<IEvaluationStore, RedisEvaluationStore>();
builder.Services.AddSingleton<ITokenStore, RedisTokenStore>();
builder.Services.AddSingleton<IOntologyStore, RedisOntologyStore>();
builder.Services.AddSingleton<IStrategyStore, RedisStrategyStore>();

// ===== JWT Auth =====
var jwtOpts = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Missing Jwt section.");

if (string.IsNullOrWhiteSpace(jwtOpts.SigningKey) || jwtOpts.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 chars.");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpts.SigningKey));

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
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<JwtIssuer>();

var app = builder.Build();

// ===== PathBase =====
if (!string.IsNullOrEmpty(pathBase))
{
    app.UsePathBase(pathBase);
}

// ===== Swagger =====
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(c =>
    {
        c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
        {
            swaggerDoc.Servers = new List<OpenApiServer>
            {
                new OpenApiServer { Url = $"{httpReq.Scheme}://{httpReq.Host.Value}{pathBase}" }
            };
        });
    });

    app.UseSwaggerUI(c =>
    {
        // Basta "swagger" → con PathBase diventa /amongus/swagger
        c.RoutePrefix = "swagger";

        // SwaggerEndpoint deve includere pathBase
        c.SwaggerEndpoint($"{pathBase}/swagger/v1/swagger.json", "SemanticAnnotator API v1");
    });

}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Seed admin
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
