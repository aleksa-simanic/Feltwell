using Feltwell.Api.Repositories;
using Feltwell.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Feltwell.Api.Tests;

public sealed class FeltwellApiFactory : WebApplicationFactory<Program>
{
    public InMemoryBlockStore Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            Remove<InMemoryBlockStore>(services);
            Remove<IBlockStore>(services);
            Remove<BlockService>(services);

            services.AddSingleton(Store);
            services.AddSingleton<IBlockStore>(Store);
            services.AddSingleton<BlockService>();
        });
    }

    private static void Remove<T>(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor is not null)
        {
            services.Remove(descriptor);
        }
    }
}
