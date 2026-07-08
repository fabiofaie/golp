using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FirebaseAdmin;
using Golp.Api.Data;
using Golp.Api.Endpoints;
using Golp.Api.Services;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Auth services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddSingleton<IEmailTemplateRenderer, EmailTemplateRenderer>();

// Email: SMTP reale se configurato (Smtp:Host), altrimenti fallback su console (dev senza credenziali)
if (!string.IsNullOrEmpty(builder.Configuration["Smtp:Host"]))
    builder.Services.AddScoped<IEmailService, SmtpEmailService>();
else
    builder.Services.AddScoped<IEmailService, DevelopmentEmailService>();

// Rating service (US-007) — ELO alla conferma partita
builder.Services.AddScoped<IRatingService, RatingService>();

// Game+Bonus rating service (US-052) — metodo alternativo, per-circolo (Circle.RatingMethod)
builder.Services.AddScoped<IGameBonusRatingService, GameBonusRatingService>();

// Sports config da DB (US-016)
builder.Services.AddScoped<ISportsService, SportsService>();

// Awards calculator + notification job (US-021)
builder.Services.AddScoped<IAwardsCalculator, AwardsCalculator>();
builder.Services.AddScoped<IAwardNotificationProcessor, AwardNotificationProcessor>();
builder.Services.AddHostedService<AwardNotificationBackgroundService>();

// Push notifications (US-006) — Firebase init solo se credenziali configurate;
// senza credenziali l'invio fallisce silenziosamente (gestito in PushNotificationService)
var firebaseJson = builder.Configuration["Firebase:ServiceAccountJson"];
var firebaseKeyPath = builder.Configuration["Firebase:ServiceAccountKeyPath"];
if (FirebaseApp.DefaultInstance == null)
{
    if (!string.IsNullOrEmpty(firebaseJson))
        FirebaseApp.Create(new AppOptions
        {
            Credential = CredentialFactory.FromJson<ServiceAccountCredential>(firebaseJson).ToGoogleCredential()
        });
    else if (!string.IsNullOrEmpty(firebaseKeyPath) && File.Exists(firebaseKeyPath))
        FirebaseApp.Create(new AppOptions
        {
            Credential = CredentialFactory.FromFile<ServiceAccountCredential>(firebaseKeyPath).ToGoogleCredential()
        });
}
builder.Services.AddSingleton<IFcmSender, FirebaseFcmSender>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();

// JWT authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var sub = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                       ?? context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var stampClaim = context.Principal?.FindFirstValue("security_stamp");

                if (sub == null || stampClaim == null || !Guid.TryParse(sub, out var userId) || !Guid.TryParse(stampClaim, out var stamp))
                {
                    context.Fail("Token non valido");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var currentStamp = await db.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.SecurityStamp)
                    .FirstOrDefaultAsync();

                if (currentStamp != stamp)
                    context.Fail("Token revocato");
            }
        };
    });

builder.Services.AddAuthorization();

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapCircleEndpoints();
app.MapMatchEndpoints();
app.MapPublicMatchEndpoints();
app.MapPushEndpoints();
app.MapAwardsEndpoints();
app.MapStatsEndpoints();
app.MapSimulateEndpoints();
app.MapSimulateGameBonusEndpoints();
app.MapQuickMatchEndpoints();
app.MapMyMatchEndpoints();

app.Run();

public partial class Program { }
