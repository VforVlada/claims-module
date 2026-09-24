using AutoMapper;
using ClaimsModule.Application.Claims.Mappings;
using ClaimsModule.Application.Common.Messaging;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Tests.Conventions;

/// <summary>Section 3.4 of the testing plan: conventions that keep the pipeline trustworthy.</summary>
public class ApplicationConventionTests
{
    private static readonly Type[] ApplicationTypes = typeof(ClaimMappingProfile).Assembly.GetTypes();

    public static IEnumerable<object[]> Commands() => ApplicationTypes
        .Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("Command", StringComparison.Ordinal)
            && t.GetInterfaces().Any(i => i == typeof(IBaseRequest)))
        .Select(t => new object[] { t });

    /// <summary>ARCH-03: every command gets validated before its handler runs — a command without a validator would skip ValidationBehavior silently.</summary>
    [Theory]
    [MemberData(nameof(Commands))]
    public void EveryCommand_HasARegisteredValidator(Type commandType)
    {
        var validatorInterface = typeof(IValidator<>).MakeGenericType(commandType);

        Assert.Contains(ApplicationTypes, t => t is { IsClass: true, IsAbstract: false } && validatorInterface.IsAssignableFrom(t));
    }

    /// <summary>Commands must opt into the transaction + post-commit event dispatch, or they'd write outside a unit of work.</summary>
    [Theory]
    [MemberData(nameof(Commands))]
    public void EveryCommand_ImplementsICommandMarker(Type commandType) =>
        Assert.True(typeof(ICommand).IsAssignableFrom(commandType), $"{commandType.Name} does not implement ICommand.");

    [Fact]
    public void CommandDiscovery_FindsTheKnownCommands() => Assert.True(Commands().Count() >= 8);

    /// <summary>ARCH-04: every destination member of every map is accounted for.</summary>
    [Fact]
    public void AutoMapperConfiguration_IsValid()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddMaps(typeof(ClaimMappingProfile).Assembly));

        configuration.AssertConfigurationIsValid();
    }
}
