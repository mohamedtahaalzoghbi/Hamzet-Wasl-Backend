using FluentValidation;
using HamzetWasl.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HamzetWasl.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<IApplicationMarker>(ServiceLifetime.Scoped);
        return services;
    }
}
