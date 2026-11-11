using HamzetWasl.Application.Interfaces;
using HamzetWasl.Infrastructure.Configuration;
using HamzetWasl.Infrastructure.Identity;
using HamzetWasl.Infrastructure.Persistence;
using HamzetWasl.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HamzetWasl.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.AddDbContext<HamzetWaslDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<HamzetWaslDbContext>()
            .AddDefaultTokenProviders();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey) && o.SigningKey.Length >= 32,
                "Jwt:SigningKey must be at least 32 characters.")
            .ValidateOnStart();

        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.SectionName));
        services.AddOptions<OtpOptions>().Bind(configuration.GetSection(OtpOptions.SectionName));
        services.AddOptions<RetentionOptions>().Bind(configuration.GetSection(RetentionOptions.SectionName));
        services.AddOptions<SeedOptions>().Bind(configuration.GetSection(SeedOptions.SectionName));
        services.AddOptions<GoogleOptions>().Bind(configuration.GetSection(GoogleOptions.SectionName));

        var redis = configuration["Redis:Configuration"];
        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redis;
                options.InstanceName = "HamzetWasl:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddMemoryCache();
        services.AddSingleton<IClock, Clock>();
        services.AddSingleton<SchemaBootstrapper>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<OtpService>();
        services.AddScoped<StorageKeyProtector>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<IEmailSender, LoggingEmailSender>();
        services.AddScoped<ISmsSender, LoggingSmsSender>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IAccessibilityService, AccessibilityService>();
        services.AddScoped<IDeletionRequestService, DeletionRequestService>();
        services.AddScoped<IGeoCatalog, GeoCatalogService>();
        services.AddScoped<IMetaService, MetaService>();
        services.AddScoped<IVolunteerService, VolunteerService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
