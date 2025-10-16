using System;
using MaterialUIHelper.Options;
using MaterialUIHelper.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MaterialUIHelper.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMaterialUiHelper(
        this IServiceCollection services,
        Action<MaterialUiHelperOptions>? configure = null)
    {
        var options = new MaterialUiHelperOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IMaterialUiHelperService, MaterialUiHelperService>();
        services.AddSingleton<IMaterialUiHelperContext, MaterialUiHelperContext>();

        return services;
    }
}