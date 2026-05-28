using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Asp.Versioning;
using Microsoft.IdentityModel.Tokens;
using Shared.Messaging;
using Users.Infrastructure;
using Users.WebHost.Consumers;
using Users.WebHost.Publishers;

var builder = WebApplication.CreateBuilder(args);

// ── Users Module ──────────────────────────────────────────────────────
builder.Services.AddUsersModule(builder.Configuration);

// ── MediatR — Users module only ──────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblies(
        typeof(Users.Infrastructure.UsersModule).Assembly,
        typeof(Users.Application.Interfaces.ICompanyService).Assembly
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

// Register RabbitMQ publishers and consumers
builder.Services.AddSingleton<UsersEventPublisher>();
builder.Services.AddHostedService<AuditLogRequestConsumer>();

// ── HTTP Clients — Fleet and Booking services ───────────────────────
var fleetServiceUrl = builder.Configuration["FleetService:BaseUrl"]
    ?? "http://localhost:5002";
builder.Services.AddHttpClient<Users.WebHost.Proxies.FleetServiceHttpClient>(client =>
{
    client.BaseAddress = new Uri(fleetServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

var bookingServiceUrl = builder.Configuration["BookingService:BaseUrl"]
    ?? "http://localhost:5003";
builder.Services.AddHttpClient<Users.WebHost.Proxies.BookingServiceHttpClient>(client =>
{
    client.BaseAddress = new Uri(bookingServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Register HTTP proxies as module API implementations (cross-service)
builder.Services.AddScoped<Shared.Contracts.Fleet.IFleetModuleApi>(sp =>
    sp.GetRequiredService<Users.WebHost.Proxies.FleetServiceHttpClient>());
builder.Services.AddScoped<Shared.Contracts.Booking.IBookingModuleApi>(sp =>
    sp.GetRequiredService<Users.WebHost.Proxies.BookingServiceHttpClient>());

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
    .AddApplicationPart(typeof(Users.Api.Controllers.Identity.AccountController).Assembly)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Host-level providers ────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Shared.Contracts.Common.ICurrentUserProvider,
    Users.WebHost.Providers.HttpContextCurrentUserProvider>();
builder.Services.AddScoped<Shared.Contracts.Common.IRequestContextProvider,
    Users.WebHost.Providers.HttpContextRequestContextProvider>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsAllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// ── Health ───────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok("healthy"));

// ── Seeding ─────────────────────────────────────────────────────────
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

    UsersModule.WaitForDatabase(sp, msg => logger.LogWarning("{Msg}", msg));

    if (config.GetValue<bool>("DataInitialization:DropDatabase"))
        UsersModule.DeleteDatabase(sp);

    if (config.GetValue<bool>("DataInitialization:MigrateDatabase"))
        UsersModule.MigrateDatabase(sp);

    if (config.GetValue<bool>("DataInitialization:SeedIdentity"))
        UsersModule.SeedIdentity(sp);

    if (config.GetValue<bool>("DataInitialization:SeedData"))
        UsersModule.SeedAppData(sp);

    if (config.GetValue<bool>("DataInitialization:SeedAppUserCompanies"))
        UsersModule.SeedAppUserCompanies(sp);
}
