# MaterialUIHelper

Translate [MUI X Data Grid](https://mui.com/x/react-data-grid/filtering/) filter models into LINQ queries in .NET.

MaterialUIHelper provides one `ApplyDataGridFilter` extension method for both `IEnumerable<T>` and `IQueryable<T>`. It is useful when a client sends a Data Grid filter model to a .NET API and the API needs to apply those filters to an in-memory collection or a query provider such as Entity Framework Core.

> [!NOTE]
> This is an early-stage library. It is not currently published as a NuGet package and some MUI filtering behavior is not implemented yet. See [Current limitations](#current-limitations) before using it in production.

## Requirements

- A project that can reference a `net8.0` library
- A .NET SDK whose compiler supports C# 14, as selected by the project file

## Installation

Clone this repository next to your application and add a project reference:

```bash
dotnet add YourApp.csproj reference ../material-ui-helper/MaterialUIHelper/MaterialUIHelper.csproj
```

You can also add the reference in your project file:

```xml
<ItemGroup>
  <ProjectReference Include="../material-ui-helper/MaterialUIHelper/MaterialUIHelper.csproj" />
</ItemGroup>
```

## Usage

MUI can send a filter model like this:

```json
{
  "items": [
    {
      "field": "Name",
      "operator": "contains",
      "value": "desk"
    },
    {
      "field": "IsActive",
      "operator": "is",
      "value": true
    }
  ],
  "logicalOperator": "And"
}
```

Deserialize the model and apply it to a query:

```csharp
using System.Text.Json;
using MaterialUIHelper.Extensions;
using MaterialUIHelper.Models.filter;

var filterJson = await request.Content.ReadAsStringAsync();
var filterModel = JsonSerializer.Deserialize<FilterModel>(filterJson)
    ?? new FilterModel();

IQueryable<Product> products = dbContext.Products;
var filteredProducts = products.ApplyDataGridFilter(filterModel, null);
```

The same API works with an in-memory collection:

```csharp
IEnumerable<Product> products = GetProducts();
var filteredProducts = products.ApplyDataGridFilter(filterModel, null);
```

`ApplyDataGridFilter` returns a filtered sequence. It does not execute an `IQueryable<T>`; execution remains deferred until the query is enumerated or materialized.

### Ignoring fields

Use `FilterConfig` to allow the client to send a field without applying it to the server-side query:

```csharp
var filterConfig = new FilterConfig
{
    FieldsToIgnore = ["ClientOnlyStatus"]
};

var filteredProducts = products.ApplyDataGridFilter(filterModel, filterConfig);
```

Field names are case-sensitive and must exactly match the corresponding public C# property name.

## Filter model

The JSON contract mirrors the relevant part of MUI's filter model:

| Property | Type | Description |
| --- | --- | --- |
| `items` | array | The filters to apply. |
| `items[].field` | string | The public property to filter. |
| `items[].operator` | string | The MUI filter operator. |
| `items[].value` | JSON value | The value used by the operator. It may be omitted for empty checks. |
| `logicalOperator` | string | Deserialized by the model, but not applied yet. |

The current implementation recognizes these operator and value combinations:

| Value kind | Operators |
| --- | --- |
| String | `is`, `not`, `equals`, `contains`, `startsWith`, `endsWith`, `isEmpty`, `isNotEmpty` |
| Date string parsed as `DateOnly` | `is`, `not`, `after`, `onOrAfter`, `before`, `onOrBefore` |
| 32-bit integer | `is`, `not` |
| Boolean | `is`, `not` |
| No value | `isEmpty`, `isNotEmpty` |
| Array of strings or 32-bit integers | `isAnyOf` |

An unrecognized operator is currently ignored. Unsupported JSON value kinds can throw `ArgumentOutOfRangeException`, and incompatible field/value types can fail while the expression is being built or executed.

## Current limitations

- Filters are applied sequentially, which gives them `And` behavior. `logicalOperator` is currently not used, so `Or` groups are not supported.
- Only direct public properties are supported; nested field paths are not resolved.
- Property names are case-sensitive.
- Numeric values are currently limited to JSON numbers that fit in `Int32`.
- Date-like strings are automatically treated as `DateOnly` values.
- `isAnyOf` expression construction needs additional work before it can be relied on.
- `isEmpty` currently builds a predicate that matches both null and non-null values.
- The relational date expressions place the filter value on the left side, so `after`/`before` ordering is currently inverted from the operator names.
- Translation support for `IQueryable<T>` depends on the query provider and the generated expression.

## Public API

```csharp
IEnumerable<T> ApplyDataGridFilter<T>(
    this IEnumerable<T> source,
    FilterModel filterModel,
    FilterConfig? filterConfig
)

IQueryable<T> ApplyDataGridFilter<T>(
    this IQueryable<T> source,
    FilterModel filterModel,
    FilterConfig? filterConfig
)
```

## Development

Build the solution from the repository root:

```bash
dotnet build MaterialUIHelper.sln
```

There is not yet an automated test project. If you extend the operator set, add coverage for both the `IEnumerable<T>` and `IQueryable<T>` paths because they use the same expression builder but execute through different LINQ providers.

## Project status

MaterialUIHelper is a small, experimental helper and its API may change. Contributions and bug reports are welcome through [GitHub issues](https://github.com/joeyfinkel/material-ui-helper/issues).
