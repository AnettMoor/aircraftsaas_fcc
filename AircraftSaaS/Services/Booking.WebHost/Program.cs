using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Asp.Versioning;
using Microsoft.IdentityModel.Tokens;
using Shared.Messaging;
using Booking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Booking Module ───────────────────────────────────────────────────
builder.Services.AddBookingModule(builder.Configuration);

// ── MediatR — Booking module only ────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblies(
        typeof(Booking.Infrastructure.BookingModule).Assembly,
        typeof(Booking.Application.Interfaces.IBookingService).Assembly
    );
});

// ── JWT Authentication ──────────────────────────────────────────────
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
builder.Services.AddAuthentication()
    .AddJwtBearer(cfg =>
    {
        cfg.RequireHttpsMetadata = false;
        cfg.SaveToken = true;
        cfg.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = builder.Configuration["JWT:Issuer"],
            ValidAudience = builder.Configuration["JWT:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["JWT:Key"]!)),
            ClockSkew = TimeSpan.Zero
        };
    });

// ── RabbitMQ ──────────────────────────────────────────────────────────
var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitPort = builder.Configuration.GetValue<int>("RabbitMQ:Port", 5672);
builder.Services.AddSingleton(new RabbitMqConnection(
    rabbitHost, rabbitPort,
    builder.Configuration["RabbitMQ:UserName"] ?? "guest",
    builder.Configuration["RabbitMQ:Password"] ?? "guest"));
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddSingleton<Booking.WebHost.Publishers.BookingEventPublisher>();
builder.Services.AddSingleton<Booking.Application.Interfaces.IBookingEventPublisher>(sp =>
    sp.GetRequiredService<Booking.WebHost.Publishers.BookingEventPublisher>());

// ── HTTP Clients — Fleet and Users services ──────────────────────────
var fleetServiceUrl = builder.Configuration["FleetService:BaseUrl"]
    ?? "http://localhost:5002";
builder.Services.AddHttpClient<Booking.WebHost.Proxies.FleetServiceHttpClient>(client =>
{
    client.BaseAddress = new Uri(fleetServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

var usersServiceUrl = builder.Configuration["UsersService:BaseUrl"]
    ?? "http://localhost:5001";
builder.Services.AddHttpClient<Booking.WebHost.Proxies.UsersServiceHttpClient>(client =>
{
    client.BaseAddress = new Uri(usersServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Register HTTP proxies as module API implementations
builder.Services.AddScoped<Shared.Contracts.Fleet.IFleetModuleApi>(sp =>
    sp.GetRequiredService<Booking.WebHost.Proxies.FleetServiceHttpClient>());
builder.Services.AddScoped<Shared.Contracts.Users.IUsersModuleApi>(sp =>
    sp.GetRequiredService<Booking.WebHost.Proxies.UsersServiceHttpClient>());

// TenantContext HTTP proxy → Users service
builder.Services.AddHttpClient<Booking.WebHost.Proxies.TenantContextProxy>(client =>
{
    client.BaseAddress = new Uri(usersServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<Shared.Contracts.Common.ITenantContext>(sp =>
    sp.GetRequiredService<Booking.WebHost.Proxies.TenantContextProxy>());

// ── API Versioning ──────────────────────────────────────────────────
builder.Services.AddApiVersioning(options =>
{
    options.ReportApiVersions = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ── Controllers ──────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddApplicationPart(typeof(Booking.Api.Controllers.BookingsController).Assembly)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Shared.Contracts.Common.ICurrentUserProvider,
    Booking.WebHost.Providers.HttpContextCurrentUserProvider>();
builder.Services.AddScoped<Shared.Contracts.Common.IRequestContextProvider,
    Booking.WebHost.Providers.HttpContextRequestContextProvider>();

// ── CORS ─────────────────────────────────────────────────────────────
// See `Users.WebHost/Program.cs` for the full rationale. Allowed
// origins are pulled from `Cors:AllowedOrigins` so each environment
// (compose / lab / OpenNebula prod) can set its own SPA host(s).
builder.Services.AddCors(options =>
{
    var allowedOrigins =
        builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    options.AddPolicy("CorsAllowAll", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .SetIsOriginAllowedToAllowWildcardSubdomains()
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("healthy"));
SetupAppData(app, app.Configuration);

// ── Migration-Job exit hook ─────────────────────────────────────────
// When the Kubernetes Migration Job runs this image it sets
// DataInitialization:ExitAfterMigrate=true so the process terminates
// cleanly once migrations + seeding are done, instead of starting the
// HTTP listener and turning the Job into a perpetual Pod.
if (app.Configuration.GetValue<bool>("DataInitialization:ExitAfterMigrate"))
{
    app.Logger.LogInformation("ExitAfterMigrate=true; shutting down after migrate/seed.");
    return;
}

app.UseCors("CorsAllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();

static void SetupAppData(WebApplication app, IConfiguration config)
{
    using var scope = app.Services.CreateScope();
    var sp = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILogger<Program>>();

    BookingModule.WaitForDatabase(sp, msg => logger.LogWarning("{Msg}", msg));

    if (config.GetValue<bool>("DataInitialization:DropDatabase"))
        BookingModule.DeleteDatabase(sp);

    if (config.GetValue<bool>("DataInitialization:MigrateDatabase"))
        BookingModule.MigrateDatabase(sp);

    if (config.GetValue<bool>("DataInitialization:SeedData"))
        BookingModule.SeedBookingData(sp);
}
