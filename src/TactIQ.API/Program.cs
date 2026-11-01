using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TactIQ.API;
using TactIQ.API.Controllers;
using TactIQ.API.Live;
using TactIQ.Application.Guests;
using TactIQ.Application.Live;
using TactIQ.Infrastructure;
using TactIQ.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTactIQInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Live sessions live in memory for the whole process, so the service is a singleton.
builder.Services.AddSingleton<ILiveMatchService, LiveMatchService>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
          ?? throw new InvalidOperationException("Jwt configuration section is missing.");

// Secrets are kept out of appsettings.json: a developer has them in the local
// appsettings.Development.json, a server in its environment variables. Empty
// means neither is set, so say which one to set instead of failing later.
if (string.IsNullOrWhiteSpace(jwt.Key))
{
    throw new InvalidOperationException(
        "Jwt:Key is not configured. Set the Jwt__Key environment variable, or copy " +
        "src/TactIQ.API/appsettings.Development.json.example to appsettings.Development.json " +
        "and fill in the values.");
}

if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Default")))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Default is not configured. Set the ConnectionStrings__Default " +
        "environment variable, or copy src/TactIQ.API/appsettings.Development.json.example " +
        "to appsettings.Development.json and fill in the values.");
}

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
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key))
        };

        // WebSockets cannot send an Authorization header, so the hub takes the token
        // from the query string instead.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs/match"))
                    context.Token = token;

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHostedService<GuestCleanupService>();

var guestOptions = builder.Configuration.GetSection(GuestOptions.SectionName).Get<GuestOptions>() ?? new GuestOptions();

// One visitor cannot open guest after guest. The count lives in memory, so a
// restart forgets it, which is fine: the cap on guests is what protects the data.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(AuthController.GuestRateLimit, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GuestRules.ClientKey(
                context.Request.Headers["X-Forwarded-For"].ToString(),
                context.Connection.RemoteIpAddress?.ToString()),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = guestOptions.PerAddressPerHour,
                Window = TimeSpan.FromHours(1)
            }));

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests",
                Detail = "Çox sayda qonaq hesabı açıldı. Bir az sonra yenidən cəhd edin."
            },
            ct);
    };
});

// Only a developer's Vite server is on another origin. In production the API
// serves the pages itself, so there is nothing for CORS to allow.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy => policy
            .WithOrigins("http://localhost:5173", "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "TactIQ API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = []
    });
});

var app = builder.Build();

await app.MigrateDatabaseAsync();

app.UseExceptionHandler();

// Open everywhere on purpose: the live site shows it to technical reviewers.
app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    // The host terminates TLS and forwards plain HTTP, so in production this
    // redirect would loop.
    app.UseHttpsRedirection();
    app.UseCors();
}
else
{
    // The built frontend, which the Docker image places in wwwroot.
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();

// A guest's token outlives their account, so each request checks the account is
// still there and notes the visit. A refused guest lands back on the login page.
app.Use(async (context, next) =>
{
    if (context.User.HasClaim(AuthService.GuestClaim, "true"))
    {
        var guests = context.RequestServices.GetRequiredService<IGuestService>();
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId is null || !await guests.TouchAsync(userId, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }

    await next();
});

app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<MatchHub>("/hubs/match");

// Answers without touching the database, so an uptime check can keep the
// server awake while the database goes on sleeping.
app.MapGet("/health", () => Results.Ok("ok"));

if (!app.Environment.IsDevelopment())
{
    // Any other path belongs to React Router, so it gets index.html. A wrong
    // API path must stay a 404: a client asking for JSON cannot read a page.
    app.MapFallback("/api/{**rest}", () => Results.NotFound());
    app.MapFallback("/hubs/{**rest}", () => Results.NotFound());
    app.MapFallbackToFile("index.html");
}

app.Run();
