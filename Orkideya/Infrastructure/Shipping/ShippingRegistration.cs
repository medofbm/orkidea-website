using Orkideya.Application.Shipping;

namespace Orkideya.Infrastructure.Shipping;

public static class ShippingRegistration
{
    public static IServiceCollection AddCarrierReadGateway(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SabilReadOptions>(configuration.GetSection(SabilReadOptions.Section));
        services.AddHttpClient<ICarrierReadGateway, SabilReadClient>(http => http.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            .RemoveAllLoggers(); // Tracking references and authentication must not enter factory request logs.
        return services;
    }
}
