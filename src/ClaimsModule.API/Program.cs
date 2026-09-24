using System.Text;
using ClaimsModule.API.Configuration;
using ClaimsModule.API.HealthChecks;
using ClaimsModule.API.Middleware;
using ClaimsModule.Application;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Infrastructure;
using ClaimsModule.Infrastructure.Auth;
using ClaimsModule.Infrastructure.BackgroundJobs;
using ClaimsModule.Persistence;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
// Empty-bodied error responses (unknown route 404, 401, 403 from [Authorize]) get a ProblemDetails
// body via UseStatusCodePages below, matching what ExceptionHandlingMiddleware emits (I-API-13).
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Claims Module API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT token obtained from POST /api/auth/mock-login",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    options.AddSecurityDefinition("Bearer", securityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [securityScheme] = [] });
});

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(CorsSettings.SectionName));
builder.Services.Configure<WorkflowSettings>(builder.Configuration.GetSection(WorkflowSettings.SectionName));

const string ApiClientPolicy = "ApiClient";
builder.Services.AddCors(options => options.AddPolicy(ApiClientPolicy, policy =>
{
    var allowedOrigins = builder.Configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()?.AllowedOrigins
        ?? new CorsSettings().AllowedOrigins;
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<StorageHealthCheck>("storage");

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages();

// Swagger stays available in every environment (S-02, I-API-14 — smoke tests hit it post-deploy).
// The Hangfire dashboard is the one thing that stays Development-only (I-JOB-11): it's a live
// operational control surface, not read-only API documentation.
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors(ApiClientPolicy);

if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire");
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Services.GetRequiredService<IRecurringJobManager>().RegisterRecurringJobs();

app.Run();

/// <summary>Exposes Program to WebApplicationFactory&lt;Program&gt; in the integration test project.</summary>
public partial class Program;
