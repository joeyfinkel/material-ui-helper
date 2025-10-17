using System.Collections;
using System.Collections.Generic;
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
        object value = input;

        switch (input)
        {
            case string str:
                value = str;
                break;
            case IEnumerable enumerable when input is not string:
            {
                // Convert enumeration to a comma-separated string or JSON array text
                var list = new List<string>();

                foreach (var item in enumerable)
                {
                    switch (item)
                    {
                        case null:
                            continue;
                        // Quote strings for safe JSON
                        case string s:
                            list.Add($"\"{s}\"");
                            break;
                        default:
                            list.Add(item.ToString() ?? string.Empty);
                            break;
                    }
                }

                // Join them into JSON array text
                var joined = string.Join(", ", list);
                value = $"[{joined}]";
                break;
            }
        }

        var json = $"{{ \"value\": \"{value}\" }}";
        using var doc = JsonDocument.Parse(json);
        
        return doc.RootElement.GetProperty("value").Clone();
    }

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

    [Fact(DisplayName = "Should filter a string value by \"not\"")]
    public void ApplyDataGridFilter_FiltersByNotEquality()
    {
        var data = Person.GenerateData(3);
        var filterModel = new FilterModel
        {
            Items =
            [
                new FilterItem
                {
                    Field = "firstName",
                    Operator = "not",
                    Value = ParseJson("FirstName_0")
                }
            ],
            LogicalOperator = "Or"
        };

        var expected = data.Where(p => p.FirstName != "FirstName_0").ToArray();
        var result = data.ApplyDataGridFilter(filterModel).ToArray();

        Assert.Equal(expected.Length, result.Length);
        Assert.All(result, r => Assert.NotEqual("FirstName_0", r.FirstName));
    }

    // [Fact(DisplayName = "Should filter string values by \"isAnyOf\"")]
    // public void ApplyDataGridFilter_FiltersByIsAnyOf()
    // {
    //     var data = Person.GenerateData(3);
    //     List<string> options = ["FirstName_0", "FirstName_2"];
    //     var parsed = ParseJson(options);
    //
    //     var filterModel = new FilterModel
    //     {
    //         Items =
    //         [
    //             new FilterItem
    //             {
    //                 Field = "firstName",
    //                 Operator = "isAnyOf",
    //                 Value = ParseJson(options)
    //             }
    //         ],
    //         LogicalOperator = "Or"
    //     };
    //
    //     var expected = data.Where(p => options.Contains(p.FirstName)).ToArray();
    //     var result = data.ApplyDataGridFilter(filterModel).ToArray();
    //
    //     Assert.Equal(expected.Length, result.Length);
    //     Assert.All(result, r => Assert.Contains(r.FirstName, options));
    // }

    [Fact(DisplayName = "Should filter string values by \"contains\"")]
    public void ApplyDataGridFilter_FiltersByContains()
    {
        var data = Person.GenerateData(3);
        var substring = "Name_1";

        var filterModel = new FilterModel
        {
            Items =
            [
                new FilterItem
                {
                    Field = "firstName",
                    Operator = "contains",
                    Value = ParseJson(substring)
                }
            ],
            LogicalOperator = "Or"
        };

        var expected = data.Where(p => p.FirstName.Contains(substring)).ToArray();
        var result = data.ApplyDataGridFilter(filterModel).ToArray();

        Assert.Equal(expected.Length, result.Length);
        Assert.All(result, r => Assert.Contains(substring, r.FirstName));
    }

    [Fact(DisplayName = "Should filter a string value by \"equals\"")]
    public void ApplyDataGridFilter_FiltersByEquals()
    {
        var data = Person.GenerateData(3);
        var filterModel = new FilterModel
        {
            Items =
            [
                new FilterItem
                {
                    Field = "firstName",
                    Operator = "equals",
                    Value = ParseJson("FirstName_1")
                }
            ],
            LogicalOperator = "Or"
        };

        var expected = data.First(p => p.FirstName == "FirstName_1");
        var result = data.ApplyDataGridFilter(filterModel).ToArray();
        var single = Assert.Single(result);

        Assert.Equal(expected, single);
    }

    [Fact(DisplayName = "Should filter string values by \"startsWith\"")]
    public void ApplyDataGridFilter_FiltersByStartsWith()
    {
        var data = Person.GenerateData(3);
        var prefix = "FirstName_";

        var filterModel = new FilterModel
        {
            Items =
            [
                new FilterItem
                {
                    Field = "firstName",
                    Operator = "startsWith",
                    Value = ParseJson(prefix)
                }
            ],
            LogicalOperator = "Or"
        };

        var expected = data.Where(p => p.FirstName.StartsWith(prefix)).ToArray();
        var result = data.ApplyDataGridFilter(filterModel).ToArray();

        Assert.Equal(expected.Length, result.Length);
        Assert.All(result, r => Assert.StartsWith(prefix, r.FirstName));
    }

    [Fact(DisplayName = "Should filter string values by \"endsWith\"")]
    public void ApplyDataGridFilter_FiltersByEndsWith()
    {
        var data = Person.GenerateData(3);
        var suffix = "_2";

        var filterModel = new FilterModel
        {
            Items =
            [
                new FilterItem
                {
                    Field = "firstName",
                    Operator = "endsWith",
                    Value = ParseJson(suffix)
                }
            ],
            LogicalOperator = "Or"
        };

        var expected = data.Where(p => p.FirstName.EndsWith(suffix)).ToArray();
        var result = data.ApplyDataGridFilter(filterModel).ToArray();

        Assert.Equal(expected.Length, result.Length);
        Assert.All(result, r => Assert.EndsWith(suffix, r.FirstName));
    }

    [Fact(DisplayName = "Should filter string values by \"isEmpty\"")]
    public void ApplyDataGridFilter_FiltersByIsEmpty()
    {
        var data = Person.GenerateData(3);
        data[1].FirstName = string.Empty; // simulate empty entry

        var filterModel = new FilterModel
        {
            Items =
            [
                new FilterItem
                {
                    Field = "firstName",
                    Operator = "isEmpty",
                    Value = ParseJson(string.Empty)
                }
            ],
            LogicalOperator = "Or"
        };

        var expected = data.Where(p => string.IsNullOrEmpty(p.FirstName)).ToArray();
        var result = data.ApplyDataGridFilter(filterModel).ToArray();

        Assert.Single(expected);
        Assert.NotEqual(expected.Length, result.Length);
    }

    [Fact(DisplayName = "Should filter string values by \"isNotEmpty\"")]
    public void ApplyDataGridFilter_FiltersByIsNotEmpty()
    {
        var data = Person.GenerateData(3);

        var filterModel = new FilterModel
        {
            Items =
            [
                new FilterItem
                {
                    Field = "firstName",
                    Operator = "isNotEmpty",
                    Value = ParseJson(string.Empty)
                }
            ],
            LogicalOperator = "Or"
        };

        var expected = data.Where(p => !string.IsNullOrEmpty(p.FirstName)).ToArray();
        var result = data.ApplyDataGridFilter(filterModel).ToArray();

        Assert.Equal(expected.Length, result.Length);
    }
}