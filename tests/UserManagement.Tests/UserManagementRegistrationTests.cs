using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using UserManagement.Client;

namespace UserManagement.Tests;

public sealed class UserManagementRegistrationTests
{
    [Fact]
    public void AddUserManagement_RegistersClientCacheAndRevisionPoller()
    {
        IConfiguration configuration = Configure();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestEnvironment());

        services.AddUserManagement(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(RedisUserResolutionCache));
        Assert.Equal("Gateway", provider.GetRequiredService<IOptions<UserManagementClientOptions>>().Value
            .GatewayIdentityAuthenticationType);
        Assert.Contains(services, descriptor => descriptor.ImplementationType == typeof(UserManagementRevisionPoller));
    }

    [Theory]
    [InlineData("UserManagement:GatewayIdentityAuthenticationType")]
    [InlineData("UserManagement:UnitOrganizationIdClaimType")]
    [InlineData("UserManagement:UnitOrganizationNameClaimType")]
    [InlineData("UserManagement:RequiredDelegatedScope")]
    public void AddUserManagement_RejectsMissingSecurityConfiguration(string key)
    {
        IConfiguration configuration = Configure(new Dictionary<string, string?> { [key] = "" });
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestEnvironment());
        services.AddUserManagement(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<UserManagementClientOptions>>().Value);
    }

    private static IConfiguration Configure(Dictionary<string, string?>? overrides = null)
    {
        Dictionary<string, string?> settings = new()
        {
            ["UserManagement:ServiceUrl"] = "https://user-management.example/",
            ["UserManagement:ApiKey"] = "test-client-key",
            ["UserManagement:GatewayIdentityAuthenticationType"] = "Gateway",
            ["UserManagement:UnitOrganizationIdClaimType"] = "unit_organization_id",
            ["UserManagement:UnitOrganizationNameClaimType"] = "unit_organization_name",
            ["UserManagement:RequiredDelegatedScope"] = "user_impersonation"
        };
        if (overrides is not null)
            foreach ((string key, string? value) in overrides) settings[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
