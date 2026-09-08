using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Vanos.API.Data;
using Vanos.API.Hubs;
using Vanos.API.Models;
using Vanos.API.Services;

var builder = WebApplication.CreateBuilder(args);

var key = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(key) && builder.Environment.IsDevelopment())
{
    key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    builder.Configuration["Jwt:Key"] = key;
}

if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
    throw new InvalidOperationException("Configure Jwt:Key (32+ bytes) via user-secrets ou variável de ambiente.");

if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"]) ||
    string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"]))
    throw new InvalidOperationException("Configure Jwt:Issuer e Jwt:Audience.");

builder.Services.AddProblemDetails();
builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// --- CORREÇÃO 1: Remove a política de bloqueio global e adiciona o Swagger de volta ---
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// --------------------------------------------------------------------------------------

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = false;
    options.MaximumReceiveMessageSize = 8192;
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (context.Request.Path.StartsWithSegments("/hubs/tracking") &&
                    !string.IsNullOrEmpty(token)) context.Token = token.ToString();
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var principal = context.Principal!;
                if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || id <= 0)
                {
                    context.Fail("Identificador inválido.");
                    return;
                }
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var account = await db.Users.AsNoTracking().SingleOrDefaultAsync(
                    u => u.Id == id, context.HttpContext.RequestAborted);
                if (account is null || !principal.IsInRole(account.Role) ||
                    (account.Role != Roles.Driver && account.Role != Roles.Parent && account.Role != Roles.Student))
                {
                    context.Fail("Conta ou role inválida.");
                    return;
                }
                if (account.Role == Roles.Driver &&
                    (!int.TryParse(principal.FindFirstValue("driverId"), out var driverId) ||
                     driverId <= 0 || account.DriverId != driverId))
                    context.Fail("Vínculo de motorista inválido.");
            }
        };
    });

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<TrackingHub>("/hubs/tracking", options => options.CloseOnAuthenticationExpiration = true);

app.Run();

public partial class Program { }