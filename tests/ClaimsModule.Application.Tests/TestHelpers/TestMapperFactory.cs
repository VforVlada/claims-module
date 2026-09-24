using AutoMapper;
using ClaimsModule.Application.Claims.Mappings;

namespace ClaimsModule.Application.Tests.TestHelpers;

public static class TestMapperFactory
{
    public static IMapper Create()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddMaps(typeof(ClaimMappingProfile).Assembly));
        return configuration.CreateMapper();
    }
}
