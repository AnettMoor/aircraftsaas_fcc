using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Asp.Versioning;
using Microsoft.IdentityModel.Tokens;
using Shared.Messaging;
using Fleet.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Fleet Module ─────────────────────────────────────────────────────
builder.Services.AddFleetModule(builder.Configuration);

// ── MediatR — Fleet module only ──────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblies(
        typeof(Fleet.Infrastructure.FleetModule).Assembly,
        typeof(Fleet.Application.Interfaces.IAircraftService).Assembly
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

// RabbitMQ consumers — booking events from Booking microservice
builder.Services.AddHostedService<Fleet.WebHost.Consumers.BookingCreatedConsumer>();
builder.Services.AddHostedService<Fleet.WebHost.Consumers.BookingCancelledConsumer>();
builder.Services.AddHostedService<Fleet.WebHost.Consumers.BookingCompletedConsumer>();

// ── HTTP Client for Booking service ──────────────────────────────────
var bookingServiceUrl = builder.Configuration["BookingService:BaseUrl"]
    ?? "http://localhost:5003";
builder.Services.AddHttpClient<Fleet.WebHost.Proxies.BookingServiceHttpClient>(client =>
{
    client.BaseAddress = new Uri(bookingServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<Shared.Contracts.Booking.IBookingModuleApi>(sp =>
    sp.GetRequiredService<Fleet.WebHost.Proxies.BookingServiceHttpClient>());

// ── HTTP Client for Users service ────────────────────────────────────
var usersServiceUrl = builder.Configuration["UsersService:BaseUrl"]
    ?? "http://localhost:5001";
builder.Services.AddHttpClient<Fleet.WebHost.Proxies.UsersServiceHttpClient>(client =>
{
    client.BaseAddress = new Uri(usersServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<Shared.Contracts.Users.IUsersModuleApi>(sp =>
    sp.GetRequiredService<Fleet.WebHost.Proxies.UsersServiceHttpClient>());

// TenantContext HTTP proxy → Users service
builder.Services.AddHttpClient<Fleet.WebHost.Proxies.TenantContextProxy>(client =>
{
    client.BaseAddress = new Uri(usersServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<Shared.Contracts.Common.ITenantContext>(sp =>
    sp.GetRequiredService<Fleet.WebHost.Proxies.TenantContextProxy>());

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
    .AddApplicationPart(typeof(Fleet.Api.Controllers.AircraftController).Assembly)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Shared.Contracts.Common.ICurrentUserProvider,
    Fleet.WebHost.Providers.HttpContextCurrentUserProvider>();
builder.Services.AddScoped<Shared.Contracts.Common.IRequestContextProvider,
    Fleet.WebHost.Providers.HttpContextRequestContextProvider>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsAllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("healthy"));
SetupAppData(app, app.Configuration);

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

    FleetModule.WaitForDatabase(sp, msg => logger.LogWarning("{Msg}", msg));

    if (config.GetValue<bool>("DataInitialization:DropDatabase"))
        FleetModule.DeleteDatabase(sp);

    if (config.GetValue<bool>("DataInitialization:MigrateDatabase"))
        FleetModule.MigrateDatabase(sp);

    if (config.GetValue<bool>("DataInitialization:SeedData"))
    {
        var companyBySlug = FetchCompanySlugMapping(config, logger);
        FleetModule.SeedFleetData(sp, companyBySlug);
    }
}

static Dictionary<string, Guid> FetchCompanySlugMapping(IConfiguration config, ILogger logger)
{
    var usersServiceUrl = config["UsersService:BaseUrl"] ?? "http://localhost:5001";
    try
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(usersServiceUrl), Timeout = TimeSpan.FromSeconds(15)
        };
        var response = httpClient.GetAsync("api/v1/internal/tenant/company-slug-mapping")
            .GetAwaiter().GetResult();
        if (response.IsSuccessStatusCode)
        {
            var mapping = response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>()
                .GetAwaiter().GetResult();
            if (mapping != null && mapping.Count > 0) return mapping;
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not reach Users service for slug mapping");
    }
    return new Dictionary<string, Guid>();
}
