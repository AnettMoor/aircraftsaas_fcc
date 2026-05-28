using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Messaging;
using Swashbuckle.AspNetCore.SwaggerGen;
using Users.Infrastructure;
using WebApp;
using WebApp.Consumers;
using WebApp.Middleware;
using WebApp.Proxies;
using WebApp.Publishers;

var builder = WebApplication.CreateBuilder(args);

// Set default culture for LangStr support - must be done before any seeding
var defaultCulture = new CultureInfo("en");
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

// ═══════════════════════════════════════════════════════════════════════════
// MODULE REGISTRATIONS
// ═══════════════════════════════════════════════════════════════════════════

// Users module only — for Identity cookie auth (SystemAdmin panel)
// Fleet and Booking modules are now separate microservices (ports 5002, 5003)
builder.Services.AddUsersModule(builder.Configuration);

// ═══════════════════════════════════════════════════════════════════════════
// HOST-LEVEL SERVICES (cross-cutting concerns)
// ═══════════════════════════════════════════════════════════════════════════

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Data Protection — use filesystem keys (UsersDbContext doesn't implement IDataProtectionKeyContext)
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), "dataprotection-keys")))
    .SetApplicationName("AircraftSaaS");

// ── JWT Bearer Authentication ───────────────────────────────────────────
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
// Do NOT override default auth scheme — let AddIdentity's cookie scheme remain the default
// for MVC/Razor pages. API controllers explicitly specify JwtBearerDefaults.AuthenticationScheme.
builder.Services
    .AddAuthentication()
    .AddJwtBearer(cfg =>
    {
        cfg.RequireHttpsMetadata = false; // TODO: set to true in production!
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

// ── Localization / Culture ──────────────────────────────────────────────
var supportedCultures = builder.Configuration
    .GetSection("SupportedCultures")
    .GetChildren()
    .Select(x => new CultureInfo(x.Value!))
    .ToArray();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.DefaultRequestCulture = new RequestCulture("en", "en");
    options.SetDefaultCulture("en");

    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider()
    };
});

// ── CORS ────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsAllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("X-Version", "X-Version-Created-At");
    });
});

// ── API Versioning ──────────────────────────────────────────────────────
var apiVersioningBuilder = builder.Services.AddApiVersioning(options =>
{
    options.ReportApiVersions = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
});

apiVersioningBuilder.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ── Swagger / OpenAPI ───────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
builder.Services.AddSwaggerGen();

builder.Services.AddLocalization();

// ── MVC & JSON ──────────────────────────────────────────────────────────
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews(options =>
    {
        options.ModelBinderProviders.Insert(0, new WebApp.ModelBinders.InvariantDateTimeModelBinderProvider());
    })
    // Fleet.Api and Booking.Api controllers are now in their own microservices
    .AddApplicationPart(typeof(Users.Api.Controllers.Identity.AccountController).Assembly)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// ── Host-level providers ────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Shared.Contracts.Common.ICurrentUserProvider, WebApp.Providers.HttpContextCurrentUserProvider>();
builder.Services.AddScoped<Shared.Contracts.Common.IRequestContextProvider, WebApp.Providers.HttpContextRequestContextProvider>();

// ═══════════════════════════════════════════════════════════════════════════
// HTTP CLIENT — for calling Users microservice
// ═══════════════════════════════════════════════════════════════════════════

var usersServiceUrl = builder.Configuration["UsersService:BaseUrl"]
    ?? "http://localhost:5001";

builder.Services.AddHttpClient<UsersServiceHttpClient>(client =>
{
    client.BaseAddress = new Uri(usersServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<TenantContextProxy>(client =>
{
    client.BaseAddress = new Uri(usersServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<AdminServiceProxy>(client =>
{
    client.BaseAddress = new Uri(usersServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<CompanyServiceProxy>(client =>
{
    client.BaseAddress = new Uri(usersServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// ═══════════════════════════════════════════════════════════════════════════
// HTTP CLIENTS — Fleet and Booking microservices
// ═══════════════════════════════════════════════════════════════════════════

var fleetServiceUrl = builder.Configuration["FleetService:BaseUrl"]
    ?? "http://localhost:5002";
var bookingServiceUrl = builder.Configuration["BookingService:BaseUrl"]
    ?? "http://localhost:5003";

builder.Services.AddHttpClient<FleetAdminServiceProxy>(client =>
{
    client.BaseAddress = new Uri(fleetServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<BookingAdminServiceProxy>(client =>
{
    client.BaseAddress = new Uri(bookingServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// ═══════════════════════════════════════════════════════════════════════════
// PROXY REGISTRATIONS — override in-process implementations with HTTP proxies
// All modules now communicate via HTTP proxies to their respective microservices.
// ═══════════════════════════════════════════════════════════════════════════

// Users proxies (existing)
builder.Services.AddScoped<Shared.Contracts.Users.IUsersModuleApi>(sp =>
    sp.GetRequiredService<UsersServiceHttpClient>());
builder.Services.AddScoped<Shared.Contracts.Common.ITenantContext>(sp =>
    sp.GetRequiredService<TenantContextProxy>());
builder.Services.AddScoped<Users.Application.Interfaces.ISystemAdminUsersService>(sp =>
    sp.GetRequiredService<AdminServiceProxy>());
builder.Services.AddScoped<Users.Application.Interfaces.ICompanyService>(sp =>
    sp.GetRequiredService<CompanyServiceProxy>());

// Fleet admin proxy (NEW — replaces in-process ISystemAdminFleetService)
builder.Services.AddScoped<Fleet.Application.Interfaces.ISystemAdminFleetService>(sp =>
    sp.GetRequiredService<FleetAdminServiceProxy>());

// Booking admin proxy (NEW — replaces in-process ISystemAdminBookingService)
builder.Services.AddScoped<Booking.Application.Interfaces.ISystemAdminBookingService>(sp =>
    sp.GetRequiredService<BookingAdminServiceProxy>());

// ═══════════════════════════════════════════════════════════════════════════
// RABBITMQ
// ═══════════════════════════════════════════════════════════════════════════

var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
builder.Services.AddSingleton(new RabbitMqConnection(
    rabbitHost,
    builder.Configuration.GetValue<int>("RabbitMQ:Port", 5672),
    builder.Configuration["RabbitMQ:UserName"] ?? "guest",
    builder.Configuration["RabbitMQ:Password"] ?? "guest"));
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddSingleton<MonolithEventPublisher>();

// RabbitMQ consumers — listen for events from Users microservice
builder.Services.AddHostedService<UserRegisteredConsumer>();
builder.Services.AddHostedService<CompanyCreatedConsumer>();
builder.Services.AddHostedService<CompanyUpdatedConsumer>();
builder.Services.AddHostedService<UserCompanyChangedConsumer>();

// ==============================================
var app = builder.Build();
// ============================================== PIPELINE ===============================

// ── Forwarded Headers (for reverse proxy at itcollege.ee) ───────────────
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// ── Health endpoint (for Docker HEALTHCHECK) ────────────────────────────
app.MapGet("/Health", () => Results.Ok("healthy"));

try
{
    SetupAppData(app, app.Environment, app.Configuration);
}
catch (Exception ex)
{
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
    startupLogger.LogCritical(ex, "FATAL: SetupAppData failed. The application will start without database initialization.");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRequestLocalization(options: app.Services
    .GetService<IOptions<RequestLocalizationOptions>>()!.Value);

app.UseCors("CorsAllowAll");

// Static files must be served BEFORE authentication/authorization so that
// uploaded photos (wwwroot/uploads) and other assets are accessible without tokens.
app.UseStaticFiles();
app.MapStaticAssets();

app.UseRouting();

app.UseAuthentication();

// Tenant resolution must run AFTER authentication so context.User is populated for cookie-based auth
app.UseTenantResolution();

app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
    foreach (var description in provider.ApiVersionDescriptions)
    {
        options.SwaggerEndpoint(
            $"/swagger/{description.GroupName}/swagger.json",
            description.GroupName.ToUpperInvariant()
        );
    }
});

// Ensure uploads directory exists for aircraft photos
var uploadsPath = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "aircraft");
Directory.CreateDirectory(uploadsPath);

app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=SystemAdmin}/{action=Dashboard}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.Run();

return;

// ═══════════════════════════════════════════════════════════════════════════
// Data seeding — delegates to module-level methods (DbContexts are internal)
// ═══════════════════════════════════════════════════════════════════════════

// WebApp no longer manages Fleet or Booking databases.
// Fleet.WebHost and Booking.WebHost handle their own database initialization.
// This method is kept as a placeholder for any future WebApp-specific startup logic.
static void SetupAppData(IApplicationBuilder app, IWebHostEnvironment env, IConfiguration configuration)
{
    using var serviceScope = app.ApplicationServices
        .GetRequiredService<IServiceScopeFactory>()
        .CreateScope();
    var sp = serviceScope.ServiceProvider;
    var logger = sp.GetRequiredService<ILogger<IApplicationBuilder>>();

    logger.LogInformation("WebApp startup — Fleet and Booking databases are managed by their respective microservices");
}
