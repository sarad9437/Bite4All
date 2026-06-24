using System.Text;
using Bite4All.API.Authentication;
using Bite4All.API.Hubs;
using Bite4All.API.Middleware;
using Bite4All.API.Services;
using Bite4All.API.Validators;
using Bite4All.Application.Queries.Reports;
using Bite4All.Application.Services;
using Bite4All.Domain.Repositories;
using Bite4All.Infrastructure;
using Bite4All.Infrastructure.Identity;
using Bite4All.Infrastructure.Repositories;
using Bite4All.Infrastructure.Services;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.Key) ||
    jwtOptions.Key.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase) ||
    Encoding.UTF8.GetByteCount(jwtOptions.Key) < 32)
{
    throw new InvalidOperationException("Jwt:Key must be configured with a private value of at least 32 bytes. Use appsettings.Development.json locally or an environment variable in deployed environments.");
}

builder.Services.AddDbContext<Bite4AllContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<Bite4AllContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

        // Allow SignalR hub connections to pass the JWT token via query string,
        // since WebSocket connections cannot set Authorization headers.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetAdminImpactQuery).Assembly));
builder.Services.AddCors(options =>
{
    options.AddPolicy("Bite4AllClient", policy =>
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// Infrastructure
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Application services
builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddScoped<IImpactReportService, ImpactReportService>();
builder.Services.AddScoped<IFoodOfferService, FoodOfferService>();
builder.Services.AddScoped<ICsvExportService, CsvExportService>();  // Fix: was missing from DI registration

// API services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Register all validators from the API assembly (CreateFoodOfferRequestValidator,
// UpdateFoodOfferRequestValidator, AssignPickupRequestValidator, CompletePickupRequestValidator, etc.)
builder.Services.AddValidatorsFromAssemblyContaining<CreateFoodOfferRequestValidator>();

builder.Services.AddScoped<INotificationPublisher, SignalRNotificationPublisher>();
builder.Services.AddHostedService<RecurrentDonationScheduler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    await app.Services.SeedIdentityAsync();
}

app.UseMiddleware<IdempotencyMiddleware>();
app.UseCors("Bite4AllClient");
app.UseHttpsRedirection();

// Ensure wwwroot exists so StaticFiles middleware does not throw on startup
// when no files have been uploaded yet.
var wwwroot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(wwwroot);
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationsHub>("/hubs/notifications");

app.Run();

public partial class Program;
