using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Infrastructure.Auth;
using ClaimsModule.Infrastructure.BackgroundJobs;
using ClaimsModule.Infrastructure.Services;
using ClaimsModule.Infrastructure.Storage;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services.Configure<StorageSettings>(configuration.GetSection(StorageSettings.SectionName));
        services.AddSingleton<IStorageService>(sp => StorageServiceFactory.Create(
            sp.GetRequiredService<IOptions<StorageSettings>>(),
            sp.GetRequiredService<IHostEnvironment>().IsDevelopment(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(StorageServiceFactory))));

        services.AddScoped<IBackgroundJobScheduler, HangfireBackgroundJobScheduler>();
        services.AddScoped<PostGlReserveChangeJob>();
        services.AddScoped<SlaMonitoringJob>();

        var connectionString = configuration.GetConnectionString("ClaimsDatabase")
            ?? throw new InvalidOperationException("Connection string 'ClaimsDatabase' was not found.");

        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));

        // Integration tests turn the processing server off (Hangfire:ServerEnabled=false) so an
        // enqueued job stays queued and can be asserted on, instead of racing the test.
        if (configuration.GetValue("Hangfire:ServerEnabled", true))
        {
            services.AddHangfireServer();
        }

        return services;
    }
}
