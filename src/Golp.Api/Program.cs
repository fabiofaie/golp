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
builder.Services.AddScoped<IEmailService, DevelopmentEmailService>();

// Rating service (US-007) — ELO alla conferma partita
builder.Services.AddScoped<IRatingService, RatingService>();

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
app.MapPushEndpoints();

app.Run();

public partial class Program { }
