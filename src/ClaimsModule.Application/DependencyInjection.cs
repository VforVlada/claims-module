using System.Reflection;
using ClaimsModule.Application.Common.Behaviors;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(assembly);

        // Registration order = pipeline order (outer to inner). DomainEventDispatchBehavior
        // must sit inside UnitOfWorkBehavior so event handlers' writes (audit rows) commit or
        // roll back together with the command's own changes.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainEventDispatchBehavior<,>));

        services.AddScoped<IClaimStatusTransitionValidator, ClaimStatusTransitionValidator>();
        services.AddSingleton<IReserveAuthorityEvaluator, ReserveAuthorityEvaluator>();

        return services;
    }
}
