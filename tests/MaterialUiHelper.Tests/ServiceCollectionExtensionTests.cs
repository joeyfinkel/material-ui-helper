using System.Linq.Expressions;
using MaterialUIHelper.Enums;
using MaterialUIHelper.Extensions;
using MaterialUIHelper.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MaterialUiHelper.Tests;

public class ServiceCollectionExtensionTests
{
    [Fact(DisplayName = "Registers Material UI Helper")]
    public void AddMaterialUiHelper_RegistersDependencies()
    {
        var services = new ServiceCollection();

        services.AddMaterialUiHelper();

        var provided = services.BuildServiceProvider();
        var service = provided.GetRequiredService<IMaterialUiHelperService>();

        Assert.NotNull(service);
        Assert.Empty(service.Options.Filters);
    }

    [Fact(DisplayName = "Registers Material UI Helper with custom filters")]
    public void AddMaterialUiHelper_RegistersDependencies_WithCustomFilters()
    {
        var services = new ServiceCollection();

        services.AddMaterialUiHelper(options => { options.RegisterFilter(FilterCheck.Equal, Expression.Equal); });

        var provided = services.BuildServiceProvider();
        var service = provided.GetRequiredService<IMaterialUiHelperService>();

        Assert.NotNull(service);
        Assert.NotEmpty(service.Options.Filters);
    }
}