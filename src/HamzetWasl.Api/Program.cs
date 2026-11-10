using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using HamzetWasl.Api.Serialization;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation.AspNetCore;
using HamzetWasl.Api.Filters;
using HamzetWasl.Api.Middleware;
using HamzetWasl.Api.OpenApi;
using HamzetWasl.Api.Realtime;
using HamzetWasl.Application;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Interfaces;using HamzetWasl.Infrastructure;
using HamzetWasl.Infrastructure.Configuration;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/hamzetwasl-.log", rollingInterval: RollingInterval.Day));

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 105 * 1024 * 1024;
    });

    builder.Services.Configure<FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 100 * 1024 * 1024;
    });

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddControllers(options => options.Filters.Add<RequestSizeLimitFilter>())
        .AddJsonOptions(options => JsonDefaults.Configure(options.JsonSerializerOptions));

    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddFluentValidationClientsideAdapters();

    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            // A body that exceeds the request limit is reported by the form reader as a model
            // state error. The contract documents 413 REQUEST_TOO_LARGE and no framework text.
            if (context.ModelState.Any(kv => kv.Value is not null && IsRequestTooLarge(kv.Value)))
            {
                var tooLarge = ApiResponse<object?>.Fail(
                    new ApiError(ErrorCodes.RequestTooLarge, ArabicMessages.For(ErrorCodes.RequestTooLarge), null),
                    context.HttpContext.TraceIdentifier);

                return new ObjectResult(tooLarge) { StatusCode = StatusCodes.Status413PayloadTooLarge };
            }

            var fields = context.ModelState
                .Where(kv => kv.Value is { Errors.Count: > 0 })
                .ToDictionary(
                    kv => ToCamelCaseField(kv.Key),
                    kv => kv.Value!.Errors
                        .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "بيانات غير صحيحة." : e.ErrorMessage)
                        .Distinct()
                        .ToArray());

            var response = ApiResponse<object?>.Fail(
                new ApiError(ErrorCodes.ValidationError, ArabicMessages.For(ErrorCodes.ValidationError), fields),
                context.HttpContext.TraceIdentifier);

            return new BadRequestObjectResult(response);
        };
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Hamzet Wasl API",
            Version = "v1",
            Description = "Phase 1 backend contract for Hamzet Wasl (همزة وصل)."
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the access token only; Swagger adds the Bearer prefix."
        });

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document, null)] = new List<string>()
        });

        options.CustomOperationIds(api =>
        {
            var values = api.ActionDescriptor.RouteValues;
            var controller = values.TryGetValue("controller", out var controllerValue) &&
                             !string.IsNullOrEmpty(controllerValue)
                ? controllerValue
                : "root";
            var action = values.TryGetValue("action", out var actionValue) && !string.IsNullOrEmpty(actionValue)
                ? actionValue
                : "index";
            return $"{controller}_{action}".ToLowerInvariant();
        });

        // The generated document must describe the API as it really behaves: the error
        // envelope, the public endpoints and the allowed enum values.
        options.OperationFilter<ApiContractOperationFilter>();
        options.SchemaFilter<ApiEnumSchemaFilter>();
    });

    var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
              ?? throw new InvalidOperationException("Jwt configuration is required.");
    if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
    {
        throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
    }

    JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = "sub",
                RoleClaimType = "role"
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    if (!string.IsNullOrWhiteSpace(accessToken) &&
                        context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    // Only access tokens may authenticate a request. Refresh tokens and
                    // Google registration tickets are bound to their own flow and must
                    // never be usable as a bearer credential.
                    var purpose = context.Principal?.FindFirst(TokenPurposes.Claim)?.Value;
                    if (!string.Equals(purpose, TokenPurposes.Access, StringComparison.Ordinal))
                    {
                        context.Fail("The token is not an access token.");
                    }

                    return Task.CompletedTask;
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    if (context.Response.HasStarted) return;
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    var response = ApiResponse<object?>.Fail(
                        new ApiError(ErrorCodes.Unauthorized, ArabicMessages.For(ErrorCodes.Unauthorized)),
                        context.HttpContext.TraceIdentifier);
                    await JsonSerializer.SerializeAsync(context.Response.Body, response,
                        JsonDefaults.Options, context.HttpContext.RequestAborted);
                },
                OnForbidden = async context =>
                {
                    if (context.Response.HasStarted) return;
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    var response = ApiResponse<object?>.Fail(
                        new ApiError(ErrorCodes.Forbidden, ArabicMessages.For(ErrorCodes.Forbidden)),
                        context.HttpContext.TraceIdentifier);
                    await JsonSerializer.SerializeAsync(context.Response.Body, response,
                        JsonDefaults.Options, context.HttpContext.RequestAborted);
                }
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("DisabledUser", policy => policy.RequireRole("disabled_user"));
        options.AddPolicy("Volunteer", policy => policy.RequireRole("volunteer"));
        options.AddPolicy("Organization", policy => policy.RequireRole("association", "company"));
        options.AddPolicy("Admin", policy => policy.RequireRole("admin"));
    });

    builder.Services.AddSignalR();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    builder.Services.AddScoped<INotificationPublisher, SignalRNotificationPublisher>();
    builder.Services.AddHealthChecks().AddDbContextCheck<HamzetWaslDbContext>();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        var limits = builder.Configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
                     ?? new RateLimitingOptions();

        // The partition key is the authenticated user when the request carries a token and
        // the remote IP otherwise. The rate limiter runs *after* the authentication
        // middleware so that the user identity is available (see the pipeline below).
        options.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(http),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limits.AuthPermitLimit,
                Window = TimeSpan.FromMinutes(limits.AuthWindowMinutes),
                QueueLimit = 0
            }));

        options.AddPolicy("otp", http => RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(http),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limits.OtpPermitLimit,
                Window = TimeSpan.FromMinutes(limits.OtpWindowMinutes),
                QueueLimit = 0
            }));

        options.AddPolicy("uploads", http => RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(http),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limits.UploadPermitLimit,
                Window = TimeSpan.FromHours(limits.UploadWindowHours),
                QueueLimit = 0
            }));

        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType = "application/json; charset=utf-8";

            // The contract promises a Retry-After header so clients can back off instead of
            // hammering the endpoint.
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            }

            var response = ApiResponse<object?>.Fail(
                new ApiError(ErrorCodes.RateLimited, ArabicMessages.For(ErrorCodes.RateLimited)),
                context.HttpContext.TraceIdentifier);
            await JsonSerializer.SerializeAsync(context.HttpContext.Response.Body, response,
                JsonDefaults.Options, token);
        };
    });

    static string PartitionKey(HttpContext http) =>
        http.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? http.User.FindFirstValue("sub")
        ?? http.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";

    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                  ?? ["http://localhost:3000", "http://localhost:5173"];
    builder.Services.AddCors(options => options.AddPolicy("Default", policy =>
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

    var app = builder.Build();

    // The forwarded headers must be read first: behind a reverse proxy the rate limiter and
    // the HTTPS redirection need the real client address and protocol.
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        ForwardLimit = 1
    });

    app.UseMiddleware<TraceIdMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseSerilogRequestLogging();
    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        await next();
    });

    if (app.Configuration.GetValue<bool>("Swagger:Enabled") || app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Hamzet Wasl API v1");
            options.DocumentTitle = "Hamzet Wasl API";
        });
    }

    var publicRoot = Path.Combine(app.Environment.ContentRootPath, "App_Data", "uploads", "public");
    Directory.CreateDirectory(publicRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(publicRoot),
        RequestPath = "/media"
    });

    app.UseHttpsRedirection();
    app.UseCors("Default");
    app.UseAuthentication();
    // The rate limiter runs after the authentication middleware so the policies can be
    // partitioned per user and not only per IP.
    app.UseRateLimiter();
    app.UseAuthorization();
    app.UseMiddleware<SuspendedUserMiddleware>();

    app.MapControllers();
    app.MapHub<NotificationsHub>("/hubs/notifications");
    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapGet("/", (HttpContext context) => Results.Ok(new
    {
        success = true,
        data = new { name = "Hamzet Wasl API", version = "v1" },
        error = (object?)null,
        traceId = context.TraceIdentifier
    })).AllowAnonymous().ExcludeFromDescription();

    if (!app.Configuration.GetValue<bool>("SkipDatabaseInitialization"))
    {
        using var scope = app.Services.CreateScope();

        var connectionString = app.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        await scope.ServiceProvider.GetRequiredService<SchemaBootstrapper>().EnsureAsync(connectionString);

        var db = scope.ServiceProvider.GetRequiredService<HamzetWaslDbContext>();
        await IdentitySchemaInitializer.InitializeAsync(db);
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Hamzet Wasl API terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

static string ToCamelCaseField(string key)
{
    if (string.IsNullOrWhiteSpace(key)) return key;
    var simple = key.TrimStart('$', '.');
    if (simple.Contains('.', StringComparison.Ordinal)) return simple;
    return JsonNamingPolicy.CamelCase.ConvertName(simple);
}

/// <summary>
/// Detects the model state error the form reader produces when the request body exceeds
/// the allowed size. The framework message is never returned to the client.
/// </summary>
static bool IsRequestTooLarge(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateEntry entry) =>
    entry.Errors.Any(error =>
        error.Exception is BadHttpRequestException or InvalidDataException ||
        (error.ErrorMessage?.Contains("too large", StringComparison.OrdinalIgnoreCase) ?? false));

public partial class Program;
