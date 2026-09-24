using System.Reflection;
using AutoMapper;
using ClaimsModule.API.Controllers;
using ClaimsModule.Application.Claims.Mappings;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Infrastructure.BackgroundJobs;
using ClaimsModule.Persistence;
using FluentValidation;
using MediatR;
using NetArchTest.Rules;

namespace ClaimsModule.ArchitectureTests;

/// <summary>
/// Plan section 5: the Clean Architecture dependency rule, enforced on every build rather than
/// by code review (ARCH-01..ARCH-04).
/// </summary>
public class LayeringTests
{
    private static readonly Assembly Domain = typeof(Claim).Assembly;
    private static readonly Assembly Application = typeof(ClaimMappingProfile).Assembly;
    private static readonly Assembly Persistence = typeof(ClaimsDbContext).Assembly;
    private static readonly Assembly Infrastructure = typeof(SlaMonitoringJob).Assembly;
    private static readonly Assembly Api = typeof(ClaimsController).Assembly;
    private static readonly Assembly[] All = [Domain, Application, Persistence, Infrastructure, Api];

    private static void AssertSuccess(TestResult result, string rule) =>
        Assert.True(result.IsSuccessful, $"{rule} — violated by: {string.Join(", ", result.FailingTypeNames ?? [])}");

    /// <summary>A-01: Domain depends on no other project, and on no EF Core, MediatR or ASP.NET Core package.</summary>
    [Fact]
    public void Domain_HasNoOutwardOrFrameworkDependencies()
    {
        var result = Types.InAssembly(Domain).ShouldNot().HaveDependencyOnAny(
            "ClaimsModule.Application", "ClaimsModule.Persistence", "ClaimsModule.Infrastructure", "ClaimsModule.API",
            "Microsoft.EntityFrameworkCore", "MediatR", "Microsoft.AspNetCore").GetResult();

        AssertSuccess(result, "A-01");
        var referenced = Domain.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        Assert.DoesNotContain(referenced, name => name.StartsWith("ClaimsModule.") || name.StartsWith("Microsoft.EntityFrameworkCore")
            || name.StartsWith("MediatR") || name.StartsWith("Microsoft.AspNetCore"));
    }

    /// <summary>A-02: Application depends inward only.</summary>
    [Fact]
    public void Application_DoesNotDependOnInfrastructurePersistenceOrApi()
    {
        var result = Types.InAssembly(Application).ShouldNot().HaveDependencyOnAny(
            "ClaimsModule.Persistence", "ClaimsModule.Infrastructure", "ClaimsModule.API", "Microsoft.AspNetCore", "Hangfire", "Azure.Storage").GetResult();

        AssertSuccess(result, "A-02");
    }

    [Fact]
    public void PersistenceAndInfrastructure_DoNotDependOnApi()
    {
        var result = Types.InAssemblies([Persistence, Infrastructure]).ShouldNot().HaveDependencyOn("ClaimsModule.API").GetResult();

        AssertSuccess(result, "outer layers never reach back into the host");
    }

    /// <summary>A-03: controllers only talk to MediatR — never a DbContext, the EF API, or persistence types.</summary>
    [Fact]
    public void Controllers_DoNotReferenceDbContextOrRepositories()
    {
        var result = Types.InAssembly(Api).That().HaveNameEndingWith("Controller").ShouldNot().HaveDependencyOnAny(
            "Microsoft.EntityFrameworkCore", "ClaimsModule.Persistence",
            "ClaimsModule.Application.Common.Interfaces.IApplicationDbContext",
            "ClaimsModule.Application.Common.Interfaces.IUnitOfWork",
            "ClaimsModule.Application.Common.Interfaces.IAuditLogService").GetResult();

        AssertSuccess(result, "A-03");
    }

    [Fact]
    public void Controllers_DependOnMediatorForBusinessOperations()
    {
        var controllers = Api.GetTypes().Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        var exempt = new[] { nameof(MockAuthController) };

        Assert.NotEmpty(controllers);
        Assert.All(controllers.Where(c => !exempt.Contains(c.Name)), c =>
            Assert.Contains(c.GetConstructors().SelectMany(ctor => ctor.GetParameters()), p => p.ParameterType == typeof(ISender)));
    }

    /// <summary>A-04: AutoMapper profiles live only in Application.</summary>
    [Fact]
    public void AutoMapperProfiles_LiveOnlyInApplication()
    {
        var profiles = All.SelectMany(a => a.GetTypes()).Where(t => typeof(Profile).IsAssignableFrom(t) && !t.IsAbstract).ToList();

        Assert.NotEmpty(profiles);
        Assert.All(profiles, p => Assert.True(p.Assembly == Application, $"{p.FullName} is outside Application"));
    }

    /// <summary>A-05: FluentValidation validators live only in Application.</summary>
    [Fact]
    public void Validators_LiveOnlyInApplication()
    {
        var validators = All.SelectMany(a => a.GetTypes()).Where(t => typeof(IValidator).IsAssignableFrom(t) && !t.IsAbstract).ToList();

        Assert.NotEmpty(validators);
        Assert.All(validators, v => Assert.True(v.Assembly == Application, $"{v.FullName} is outside Application"));
    }

    [Fact]
    public void Naming_HandlersValidatorsAndProfilesFollowConventions()
    {
        // Guard against vacuous passes: each selector must actually match the types it governs.
        Assert.True(Types.InAssembly(Application).That().ImplementInterface(typeof(IRequestHandler<,>)).GetTypes().Count() >= 10);
        Assert.True(Types.InAssembly(Application).That().Inherit(typeof(AbstractValidator<>)).GetTypes().Count() >= 8);
        Assert.True(Types.InAssembly(Application).That().Inherit(typeof(Profile)).GetTypes().Count() >= 3);

        AssertSuccess(Types.InAssembly(Application).That().ImplementInterface(typeof(IRequestHandler<,>))
            .Should().HaveNameEndingWith("Handler").GetResult(), "request handlers end with Handler");
        AssertSuccess(Types.InAssembly(Application).That().Inherit(typeof(AbstractValidator<>))
            .Should().HaveNameEndingWith("Validator").And().ResideInNamespaceEndingWith("Validators").GetResult(), "validators end with Validator and sit in a Validators namespace");
        AssertSuccess(Types.InAssembly(Application).That().Inherit(typeof(Profile))
            .Should().HaveNameEndingWith("MappingProfile").And().ResideInNamespaceEndingWith("Mappings").GetResult(), "profiles end with MappingProfile and sit in a Mappings namespace");
    }
}
