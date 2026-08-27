using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Application.Interfaces.Auth;
using Application.Interfaces.Users;
using Application.Services.Auth;
using Application.Services.Users;
using Application.Validators.Users;
using Core.Interfaces;
using FluentValidation;
using Infrastructure;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using WebApi.Authorization;
using WebApi.Middleware;
using WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

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
}

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

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
