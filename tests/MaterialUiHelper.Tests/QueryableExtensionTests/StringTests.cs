using System.Linq;
using System.Text.Json;
using MaterialUIHelper.Extensions;
using MaterialUIHelper.Models.filter;
using MaterialUiHelper.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MaterialUIHelper.Tests.QueryableExtensionTests;

internal class Person : TestDataGenerator<Person>
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public int Age { get; set; }
}

public class StringTests
{
    private readonly ServiceProvider _provider;

    public StringTests()
    {
        var services = new ServiceCollection();

        services.AddMaterialUiHelper();

        _provider = services.BuildServiceProvider();
    }

    private static JsonElement ParseJson<T>(T input)
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(input)).RootElement;
    }

    // private static List<Person> GenerateData()
    // {
    //     
    // }

    [Fact(DisplayName = "Should filter a string value by \"is\"")]
    public void ApplyDataGridFilter_FiltersByIsEquality()
    {
        var data = Person.GenerateData(3);
        var filterModel = new FilterModel
        {
            Items =
            [
                new FilterItem
                {
                    Field = "firstName",
                    Operator = "is",
                    Value = ParseJson("FirstName_0")
                }
            ],
            LogicalOperator = "Or"
        };
        var expected = data.First(p => p.FirstName == "FirstName_0");
        var result = data.ApplyDataGridFilter(filterModel).ToArray();
        var single = Assert.Single(result);

        Assert.Equal(expected, single);
    }
}