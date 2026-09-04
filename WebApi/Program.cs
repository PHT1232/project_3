using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Application.Interfaces.Ai;
using Application.Interfaces.Auth;
using Application.Interfaces.Catalogue;
using Application.Interfaces.Inventory;
using Application.Interfaces.Notifications;
using Application.Interfaces.Reports;
using Application.Interfaces.Requests;
using Application.Interfaces.SupplierRequests;
using Application.Interfaces.Suppliers;
using Application.Interfaces.Users;
using Application.Services.Ai;
using Application.Services.Auth;
using Application.Services.Catalogue;
using Application.Services.Inventory;
using Application.Services.Suppliers;
using Application.Services.Users;
using Application.Validators.Users;
using Core.Interfaces;
using FluentValidation;
using Infrastructure;
using Infrastructure.Ai;
using Infrastructure.Data;
using Infrastructure.Identity;
using Infrastructure.Queries;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Debugging;
using WebApi.Authorization;
using WebApi.Controllers;
using WebApi.Middleware;
using WebApi.Services;

// Serilog swallows its own sink errors (e.g. Elasticsearch connection/auth failures) by
// default — without this, a broken ES sink fails completely silently and just never writes
// anything, which is otherwise indistinguishable from "no logs happened yet".
SelfLog.Enable(msg => Console.Error.WriteLine($"[Serilog] {msg}"));

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting up Stationery Management System API");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console();

        // Skipped in Testing so WebApplicationFactory-based integration tests never make
        // network calls to an Elasticsearch instance that isn't running in CI.
        if (!context.HostingEnvironment.IsEnvironment("Testing"))
        {
            var elasticsearchUri = context.Configuration["Elasticsearch:Uri"];
            if (!string.IsNullOrWhiteSpace(elasticsearchUri))
            {
                configuration.WriteTo.Elasticsearch(new Serilog.Sinks.Elasticsearch.ElasticsearchSinkOptions(new Uri(elasticsearchUri))
                {
                    AutoRegisterTemplate = true,
                    IndexFormat = $"stationeryms-logs-{context.HostingEnvironment.EnvironmentName.ToLower()}-{{0:yyyy.MM}}",
                });
            }
        }
    },
    // WebApplicationFactory<Program> (integration tests) re-runs these top-level statements once
    // per test class, often concurrently. UseSerilog defaults to overwriting the shared static
    // Log.Logger, so one factory's finally-block Log.CloseAndFlush() was disposing the logger
    // another concurrently-starting factory was actively using, throwing ObjectDisposedException
    // mid-startup ("the entry point exited without ever building an IHost"). Keeping the static
    // logger untouched and scoping each host to its own DI-resolved logger avoids that.
    preserveStaticLogger: true);

    builder.Services.AddDbContext<DataContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services
        .AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddEntityFrameworkStores<DataContext>()
        .AddDefaultTokenProviders();

    var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
    if (string.IsNullOrWhiteSpace(jwtSigningKey))
    {
        throw new InvalidOperationException(
            "Jwt:SigningKey is not configured. Set it via appsettings.Development.json locally or the Jwt__SigningKey environment variable in every other environment.");
    }

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            // Without this, the handler remaps "sub" to ClaimTypes.NameIdentifier on validation,
            // silently breaking every claims lookup that reads JwtRegisteredClaimNames.Sub
            // (CurrentUserService, ApproverHandler, AuthController.Me, OnTokenValidated below).
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            };

            // Immediate deactivation enforcement (plan §7): JWTs are otherwise valid for up to
            // 8 hours, so check IsActive against the DB on every request rather than trusting the token.
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var sub = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                    if (sub is null || !int.TryParse(sub, out var employeeNumber))
                    {
                        context.Fail("Invalid token.");
                        return;
                    }

                    var userManager = context.HttpContext.RequestServices
                        .GetRequiredService<UserManager<ApplicationUser>>();
                    var user = await userManager.FindByIdAsync(sub);
                    if (user is null || !user.IsActive)
                    {
                        context.Fail("User is inactive or no longer exists.");
                    }
                },
            };
        });

    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("RequireManager", policy => policy.Requirements.Add(new RankLevelRequirement(2)))
        .AddPolicy("RequireApprover", policy => policy.Requirements.Add(new ApproverRequirement()));

    builder.Services.AddSingleton<IAuthorizationHandler, RankLevelHandler>();
    builder.Services.AddScoped<IAuthorizationHandler, ApproverHandler>();

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

    builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    builder.Services.AddScoped<IAccountStore, IdentityAccountAdapter>();
    builder.Services.AddScoped<ITokenService, JwtTokenService>();
    builder.Services.AddScoped<IPasswordService, IdentityPasswordService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IUserStore, IdentityUserStore>();
    builder.Services.AddScoped<IUserManagementService, UserManagementService>();

    builder.Services.AddScoped<IItemQueries, ItemQueries>();
    builder.Services.AddScoped<IItemService, ItemService>();
    builder.Services.AddScoped<ICategoryService, CategoryService>();
    builder.Services.AddScoped<ISupplierQueries, SupplierQueries>();
    builder.Services.AddScoped<ISupplierService, SupplierService>();
    builder.Services.AddScoped<IInventoryQueries, InventoryQueries>();
    builder.Services.AddScoped<IStockQueries, StockQueries>();
    builder.Services.AddScoped<IStockService, StockService>();
    builder.Services.AddScoped<IInventoryService, InventoryService>();
    builder.Services.AddScoped<ISupplierRequestQueries, SupplierRequestQueries>();
    builder.Services.AddScoped<ISupplierRequestService, SupplierRequestService>();
    builder.Services.AddScoped<IHierarchyQueries, HierarchyQueries>();
    builder.Services.AddScoped<IRequestQueries, RequestQueries>();
    builder.Services.AddScoped<IRequestService, RequestService>();
    builder.Services.AddScoped<IReportQueries, ReportQueries>();
    builder.Services.AddScoped<IEligibilityQueries, EligibilityQueries>();
    builder.Services.AddScoped<INotificationQueries, NotificationQueries>();
    builder.Services.AddScoped<INotificationService, NotificationService>();

    // AI Request Assistant (Plan §5.2, A1). The provider key is read from configuration only —
    // in every non-development environment that means the Gemini__ApiKey environment variable
    // (locally, `dotnet user-secrets`); the checked-in appsettings files carry an empty string.
    // A missing key is a warning, not a failure: the feature degrades to keyword matching
    // (Plan §5.2 rule 4).
    var aiAssistantOptions = builder.Configuration.GetSection("Ai").Get<AiAssistantOptions>() ?? new AiAssistantOptions();
    aiAssistantOptions.Enabled = builder.Configuration.GetValue("Features:AiAssistant", true);
    var geminiOptions = builder.Configuration.GetSection(GeminiOptions.SectionName).Get<GeminiOptions>() ?? new GeminiOptions();
    if (aiAssistantOptions.Enabled && string.IsNullOrWhiteSpace(geminiOptions.ApiKey))
    {
        Log.Warning("Gemini:ApiKey is not configured — the AI Request Assistant will use keyword-matching fallback only. Set the Gemini__ApiKey environment variable (or `dotnet user-secrets set Gemini:ApiKey …`) to enable the model.");
    }

    builder.Services.AddSingleton(aiAssistantOptions);
    builder.Services.AddSingleton(geminiOptions);
    // The timeout lives on the named HttpClient so the SDK-free client stays a plain POST.
    builder.Services.AddHttpClient(GeminiLlmClient.HttpClientName, client =>
    {
        client.BaseAddress = new Uri(geminiOptions.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, geminiOptions.TimeoutSeconds));
    });
    builder.Services.AddSingleton<ILlmClient, GeminiLlmClient>();
    builder.Services.AddScoped<IRequestAssistantService, RequestAssistantService>();
    builder.Services.AddScoped<IAiUsageQueries, AiUsageQueries>();

    // Plan §5.2 rule 6: 20 AI calls per user per hour. Built-in ASP.NET rate limiter, keyed by
    // the JWT subject (employee number) — no extra dependency, no state outside the process.
    var aiCallsPerHour = builder.Configuration.GetValue("Ai:RateLimitPerHour", 20);
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(AiController.RateLimitPolicy, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = aiCallsPerHour,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                }));
    });

    builder.Services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>();
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Stationery Management System API",
            Version = "v1",
            Description = "Sign-in, session, and user-management endpoints (Plan §4). "
                + "Protected routes need a bearer token — log in via POST /api/v1/auth/login, "
                + "then click Authorize and paste the accessToken.",
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT bearer token from POST /api/v1/auth/login.",
        });
        options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", null, null)] = [],
        });
    });

    var app = builder.Build();

    if (!app.Environment.IsEnvironment("Testing"))
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
        await dbContext.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        await DbSeeder.SeedRolesAsync(roleManager);

        var bootstrapAdminPassword = builder.Configuration["Seed:BootstrapAdminPassword"];
        if (string.IsNullOrWhiteSpace(bootstrapAdminPassword))
        {
            throw new InvalidOperationException(
                "Seed:BootstrapAdminPassword is not configured. Set it via appsettings.Development.json locally or the Seed__BootstrapAdminPassword environment variable in every other environment.");
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await DbSeeder.SeedBootstrapAdminAsync(userManager, bootstrapAdminPassword);
    }

    app.UseSerilogRequestLogging();

    // Enabled in every environment (not just Development) — this is a small internal eProject
    // deployment with no public exposure beyond the team, and the UI is the primary way to
    // exercise auth/user-management endpoints without a separate REST client.
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Stationery Management System API v1");
    });

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseHttpsRedirection();

    app.UseStaticFiles();

    app.UseAuthentication();
    app.UseAuthorization();

    // After authentication so the AI policy can partition on the user's employee number.
    app.UseRateLimiter();

    app.MapControllers();
    app.MapFallbackToFile("index.html");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
