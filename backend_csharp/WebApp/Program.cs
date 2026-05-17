using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using App.Infrastructure;
using App.Infrastructure.Seeding;
using App.Domain.Identity;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Swashbuckle.AspNetCore.SwaggerGen;
using WebApp;
using WebApp.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Set default culture for LangStr support - must be done before any seeding
var defaultCulture = new System.Globalization.CultureInfo("en");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// used for older style [Column(TypeName = "jsonb")] for LangStr
#pragma warning disable CS0618 // Type or member is obsolete
//NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();
#pragma warning restore CS0618 // Type or member is obsolete


builder.Services.AddDbContext<AppDbContext>(options => options
        .UseNpgsql(
            connectionString,
            o =>
            {
                o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                o.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            }
        )
        .ConfigureWarnings(w => w
            .Ignore(RelationalEventId.MultipleCollectionIncludeWarning)
            .Log(RelationalEventId.PendingModelChangesWarning)
        )
        .EnableDetailedErrors()
        .EnableSensitiveDataLogging()
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution)
    );

// Migrations are handled in SetupAppData after the app is built,
// which gives Docker networking time to stabilize.

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// using Microsoft.AspNetCore.DataProtection;
// Configure Data Protection with proper key management
builder.Services
    .AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("AircraftSaaS");

builder.Services.AddIdentity<AppUser, AppRole>(options => 
    {
        options.SignIn.RequireConfirmedAccount = false;
        // Configure password requirements to allow "3" as password
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 1;
    })
    .AddDefaultUI()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear(); // => remove default claims
// Do NOT override default auth scheme — let AddIdentity's cookie scheme remain the default
// for MVC/Razor pages. API controllers explicitly specify JwtBearerDefaults.AuthenticationScheme.

// read header, extract claims, validate
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
            ClockSkew = TimeSpan.Zero // remove delay of token when expire
        };
    });



var supportedCultures = builder.Configuration
    .GetSection("SupportedCultures")
    .GetChildren()
    .Select(x => new CultureInfo(x.Value!))
    .ToArray();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    // datetime and currency support
    options.SupportedCultures = supportedCultures;
    // UI translated strings
    options.SupportedUICultures = supportedCultures;
    // if nothing is found, use this
    options.DefaultRequestCulture = new RequestCulture("en", "en");
    options.SetDefaultCulture("en");

    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        // Order is important, it's in which order they will be evaluated
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider()
    };
});

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


var apiVersioningBuilder = builder.Services.AddApiVersioning(options =>
{
    options.ReportApiVersions = true;
    // in case of no explicit version
    options.DefaultApiVersion = new ApiVersion(1, 0);
});

apiVersioningBuilder.AddApiExplorer(options =>
{
    // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
    // note: the specified format code will format the version as "'v'major[.minor][-status]"
    options.GroupNameFormat = "'v'VVV";

    // note: this option is only necessary when versioning by url segment. the SubstitutionFormat
    // can also be used to control the format of the API version in route templates
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
builder.Services.AddSwaggerGen();

builder.Services.AddLocalization();

//register all classes inheriting controller or controllerbase
builder.Services.AddControllersWithViews(options =>
    {
        options.ModelBinderProviders.Insert(0, new WebApp.ModelBinders.InvariantDateTimeModelBinderProvider());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

//dependency injection == When a request needs IInterface, create/use Implementation.
// Register HTTP context accessor and UOW
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<App.Application.Interfaces.ICurrentUserProvider, WebApp.Providers.HttpContextCurrentUserProvider>();
builder.Services.AddScoped<App.Application.Interfaces.IRequestContextProvider, WebApp.Providers.HttpContextRequestContextProvider>();
builder.Services.AddScoped<App.Domain.Contracts.IAppUOW, App.Infrastructure.AppUOW>();
builder.Services.AddScoped<App.Domain.Contracts.IRefreshTokenRepository, App.Infrastructure.Repositories.RefreshTokenRepository>();

// Register application services
builder.Services.AddScoped<App.Application.Interfaces.ITenantService, App.Application.Services.TenantService>();
builder.Services.AddScoped<App.Application.Interfaces.IAircraftService, App.Application.Services.AircraftService>();
builder.Services.AddScoped<App.Application.Interfaces.IBookingService, App.Application.Services.BookingService>();
builder.Services.AddScoped<App.Application.Interfaces.IAirportService, App.Application.Services.AirportService>();
builder.Services.AddScoped<App.Application.Interfaces.IReviewService, App.Application.Services.ReviewService>();
builder.Services.AddScoped<App.Application.Interfaces.ICompanyService, App.Application.Services.CompanyService>();
builder.Services.AddScoped<App.Application.Interfaces.IMaintenanceService, App.Application.Services.MaintenanceService>();
builder.Services.AddScoped<App.Application.Interfaces.IAuditService, App.Application.Services.AuditService>();
builder.Services.AddScoped<App.Application.Interfaces.IInsurancePolicyService, App.Application.Services.InsurancePolicyService>();
builder.Services.AddScoped<App.Application.Interfaces.IAircraftAvailabilityService, App.Application.Services.AircraftAvailabilityService>();
builder.Services.AddScoped<App.Application.Interfaces.ILicenseService, App.Application.Services.LicenseService>();
builder.Services.AddScoped<App.Domain.Contracts.ISystemAdminService, App.Infrastructure.Services.SystemAdminService>();
builder.Services.AddScoped<App.Application.Interfaces.IPersonService, App.Application.Services.PersonService>();
builder.Services.AddScoped<App.Application.Interfaces.IContactTypeService, App.Application.Services.ContactTypeService>();
builder.Services.AddScoped<App.Application.Interfaces.IContactService, App.Application.Services.ContactService>();
builder.Services.AddScoped<App.Application.Interfaces.IAppUserCompanyService, App.Application.Services.AppUserCompanyService>();
builder.Services.AddScoped<App.Domain.Contracts.IAuthService, App.Infrastructure.Services.AuthService>();

// ==============================================
var app = builder.Build();
// ============================================== PIPELINE ===============================
SetupAppData(app, app.Environment, app.Configuration);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
    // serve from root
    // options.RoutePrefix = string.Empty;
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

static void SetupAppData(IApplicationBuilder app, IWebHostEnvironment env, IConfiguration configuration)
{
    using var serviceScope = ((IApplicationBuilder)app).ApplicationServices
        .GetRequiredService<IServiceScopeFactory>()
        .CreateScope();
    var logger = serviceScope.ServiceProvider.GetRequiredService<ILogger<IApplicationBuilder>>();

    using var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

    WaitDbConnection(context, logger);

    using var userManager = serviceScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    using var roleManager = serviceScope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

    if (configuration.GetValue<bool>("DataInitialization:DropDatabase"))
    {
        logger.LogWarning("DropDatabase");
        AppDataInit.DeleteDatabase(context);
    }

    if (configuration.GetValue<bool>("DataInitialization:MigrateDatabase"))
    {
        logger.LogInformation("MigrateDatabase");
        AppDataInit.MigrateDatabase(context);
    }

    if (configuration.GetValue<bool>("DataInitialization:SeedIdentity"))
    {
        logger.LogInformation("SeedIdentity");
        AppDataInit.SeedIdentity(userManager, roleManager);
    }

    if (configuration.GetValue<bool>("DataInitialization:SeedData"))
    {
        logger.LogInformation("SeedData");
        AppDataInit.SeedAppData(context);
    }

    if (configuration.GetValue<bool>("DataInitialization:SeedAppUserCompanies"))
    {
        logger.LogInformation("SeedAppUserCompanies");
        AppDataInit.SeedAppUserCompanies(context);
    }
}

static void WaitDbConnection(AppDbContext ctx, ILogger<IApplicationBuilder> logger)
{
    var retryCount = 0;
    const int maxRetries = 60;

    while (true)
    {
        try
        {
            ctx.Database.OpenConnection();
            ctx.Database.CloseConnection();
            return;
        }
        catch (Exception e)
        {
            retryCount++;
            // Unwrap to find the inner PostgresException if present
            var pgEx = e as Npgsql.PostgresException ?? e.InnerException as Npgsql.PostgresException;
            var message = pgEx?.Message ?? e.InnerException?.Message ?? e.Message;

            logger.LogWarning("Checked postgres db connection (attempt {Attempt}/{MaxRetries}). Got: {Message}",
                retryCount, maxRetries, message);

            if (message.Contains("does not exist"))
            {
                logger.LogWarning("Applying migration, probably db is not there (but server is)");
                return;
            }

            if (retryCount >= maxRetries)
            {
                logger.LogError("Exceeded max retries ({MaxRetries}). Giving up waiting for db connection.", maxRetries);
                throw;
            }

            // Transient errors: db in recovery mode (57P03), SocketException, DNS not ready, etc.
            logger.LogWarning("Waiting for db connection. Sleep 1 sec");
            System.Threading.Thread.Sleep(1000);
        }
    }
}