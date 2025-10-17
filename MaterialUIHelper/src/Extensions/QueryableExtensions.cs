using System.Linq.Expressions;
using System.Text.Json;
using MaterialUIHelper.Models.filter;

namespace MaterialUIHelper.Extensions;

internal enum FilterCheck
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Contains,
    StartsWith,
    EndsWith,
    IsEmpty,
    IsNotEmpty,
    IsAnyOf
}

public static class QueryableExtensions
{
    private static Func<T, TValue?> GetValue<T, TValue>(string property)
    {
        return e => (TValue?)e?.GetType().GetProperty(property)?.GetValue(e);
    }

    private static Func<T, object?> GetProperty<T, TValue>(string property)
    {
        return GetValue<T, object>(property.ToUpperFirstLetter());
    }

    private static IQueryable<T> ApplyFilter<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate)
    {
        return source.Where(predicate);
    }

    private static IEnumerable<T> ApplyFilter<T>(IEnumerable<T> source, Func<T, bool> predicate)
    {
        return source.Where(predicate);
    }

    private static dynamic FilterByPropertyDynamic<TItem, TValue>(
        dynamic source,
        string propertyName,
        TValue value,
        FilterCheck check
    )
    {
        var parameter = Expression.Parameter(typeof(TItem), "item");
        var property = Expression.Property(parameter, propertyName);

        Expression predicate;
        Expression constant;

        if (check == FilterCheck.IsAnyOf && value is IEnumerable<TValue> values)
        {
            constant = Expression.Constant(values, typeof(TValue));
            var containsMethod = source is IQueryable<TItem>
                ? typeof(Queryable).GetMethods()
                    .First(p => p.Name == nameof(Queryable.Contains) && p.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(TValue))
                : typeof(Enumerable).GetMethods()
                    .First(p => p.Name == nameof(Queryable.Contains) && p.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(TValue));

            predicate = Expression.Call(containsMethod, constant, property);
        }
        else
        {
            constant = Expression.Constant(value, typeof(TValue));

            // Create a null-check expression
            var notNullProperty = Expression.NotEqual(property, Expression.Constant(null, property.Type));
            // Make sure the constant is cast to the same type as the property (Nullable<DateTime>)
            var castedConstant =
                property.Type.IsGenericType && property.Type.GetGenericTypeDefinition() == typeof(Nullable<>)
                    ? Expression.Convert(constant, property.Type)
                    : constant;

            predicate = check switch
            {
                FilterCheck.Equal => Expression.AndAlso(notNullProperty, Expression.Equal(property, castedConstant)),
                FilterCheck.NotEqual => Expression.AndAlso(notNullProperty,
                    Expression.NotEqual(property, castedConstant)),
                FilterCheck.GreaterThan => Expression.AndAlso(notNullProperty,
                    Expression.GreaterThan(castedConstant, property)),
                FilterCheck.GreaterThanOrEqual => Expression.AndAlso(notNullProperty,
                    Expression.GreaterThanOrEqual(castedConstant, property)),
                FilterCheck.LessThan => Expression.AndAlso(notNullProperty,
                    Expression.LessThan(castedConstant, property)),
                FilterCheck.LessThanOrEqual => Expression.AndAlso(notNullProperty,
                    Expression.LessThan(castedConstant, property)),
                FilterCheck.Contains => Expression.AndAlso(notNullProperty,
                    Expression.Call(property, nameof(string.Contains), null, constant)),
                FilterCheck.StartsWith => Expression.AndAlso(notNullProperty,
                    Expression.Call(property, nameof(string.StartsWith), null, constant)),
                FilterCheck.EndsWith => Expression.AndAlso(notNullProperty,
                    Expression.Call(property, nameof(string.EndsWith), null, constant)),
                FilterCheck.IsEmpty => Expression.OrElse(notNullProperty,
                    Expression.Equal(property, Expression.Constant(null, property.Type))),
                FilterCheck.IsNotEmpty => Expression.AndAlso(
                    Expression.NotEqual(property, Expression.Constant(null, property.Type)), // Check for not null
                    Expression.NotEqual(property, Expression.Constant("")) // Check for not empty string
                ),
                FilterCheck.IsAnyOf => Expression.Call(typeof(Enumerable).GetMethods()
                        .First(p => p.Name == nameof(Enumerable.Contains) && p.GetParameters().Length == 2)
                        .MakeGenericMethod(typeof(TValue)),
                    Expression.Constant(value, typeof(IEnumerable<TValue>)),
                    property),
                _ => throw new ArgumentOutOfRangeException(nameof(check), "Invalid filter check")
            };
        }

        var lambda = Expression.Lambda<Func<TItem, bool>>(predicate, parameter);

        return source switch
        {
            IQueryable<TItem> queryable => ApplyFilter(queryable, lambda),
            IEnumerable<TItem> enumerable => ApplyFilter(enumerable, lambda.Compile()),
            _ => throw new ArgumentException(
                $"The source type {source.GetType()} is not supported. Supported types include IQueryable<T> or IEnumerable<T>.")
        };
    }

    private static IEnumerable<TItem> FilterByProperty<TItem, TValue>(this IEnumerable<TItem> source,
        string propertyName, TValue value, FilterCheck check) =>
        FilterByPropertyDynamic<TItem, TValue>(source, propertyName, value, check);

    private static IQueryable<TItem> FilterByProperty<TItem, TValue>(this IQueryable<TItem> source,
        string propertyName, TValue value, FilterCheck check) =>
        FilterByPropertyDynamic<TItem, TValue>(source, propertyName, value, check);

    private static bool ShouldSkipFilter(FilterItem item)
    {
        if (item.Operator is "isEmpty" or "isNotEmpty")
        {
            return false;
        }

        return item.Value.ValueKind == JsonValueKind.Undefined;
    }

    private static bool ShouldSkipFiltering(FilterModel? model)
    {
        if (model is null)
        {
            return true;
        }

        if (model.Items.Count() != 1)
        {
            return false;
        }

        var first = model.Items.FirstOrDefault();

        return first is not null && ShouldSkipFilter(first);
    }

    private static dynamic Filter(dynamic query, FilterModel model, FilterConfig? config = null)
    {
        if (ShouldSkipFiltering(model))
        {
            return query;
        }

        foreach (var item in model.Items)
        {
            if (config is not null && config.FieldsToIgnore.Contains(item.Field))
            {
                continue;
            }

            switch (item.Value.ValueKind)
            {
                case JsonValueKind.Undefined:
                    query = item.Operator switch
                    {
                        "isEmpty" => FilterByProperty(query, item.Field, null, FilterCheck.IsEmpty),
                        "isNotEmpty" => FilterByProperty(query, item.Field, null, FilterCheck.IsNotEmpty),
                        _ => query
                    };
                    break;
                case JsonValueKind.Array:
                    query = item.Operator switch
                    {
                        "isAnyOf" => FilterByProperty(query, item.Field,
                            item.Value.EnumerateArray().Select(p => (object)(p.ValueKind switch
                            {
                                JsonValueKind.String => p.GetString() ?? "",
                                JsonValueKind.Number => p.GetInt32(),
                                _ => throw new ArgumentOutOfRangeException($"{p.ValueKind} is not supported.")
                            })),
                            FilterCheck.IsAnyOf),
                        _ => query
                    };
                    break;
                case JsonValueKind.String when item.Value.GetString() is { } value:
                    if (DateOnly.TryParse(value, out var dateOnly))
                    {
                        query = item.Operator switch
                        {
                            "is" => FilterByProperty(query, item.Field, dateOnly, FilterCheck.Equal),
                            "not" => FilterByProperty(query, item.Field, dateOnly, FilterCheck.NotEqual),
                            "after" => FilterByProperty(query, item.Field, dateOnly, FilterCheck.GreaterThan),
                            "onOrAfter" => FilterByProperty(query, item.Field, dateOnly,
                                FilterCheck.GreaterThanOrEqual),
                            "before" => FilterByProperty(query, item.Field, dateOnly, FilterCheck.LessThan),
                            "onOrBefore" => FilterByProperty(query, item.Field, dateOnly, FilterCheck.LessThanOrEqual),
                            _ => query
                        };
                    }
                    else
                    {
                        query = item.Operator switch
                        {
                            "is" => FilterByProperty(query, item.Field, value, FilterCheck.Equal),
                            "not" => FilterByProperty(query, item.Field, value, FilterCheck.NotEqual),
                            "isAnyOf" => FilterByProperty(query, item.Field, value, FilterCheck.IsAnyOf),
                            "contains" => FilterByProperty(query, item.Field, value, FilterCheck.Contains),
                            "equals" => FilterByProperty(query, item.Field, value, FilterCheck.Equal),
                            "startsWith" => FilterByProperty(query, item.Field, value, FilterCheck.StartsWith),
                            "endsWith" => FilterByProperty(query, item.Field, value, FilterCheck.EndsWith),
                            "isEmpty" => FilterByProperty(query, item.Field, value, FilterCheck.IsEmpty),
                            "isNotEmpty" => FilterByProperty(query, item.Field, value, FilterCheck.IsNotEmpty),
                            _ => query
                        };
                    }

                    break;
                case JsonValueKind.Number when item.Value.TryGetInt32(out var value):
                    query = item.Operator switch
                    {
                        "is" => FilterByProperty(query, item.Field, value, FilterCheck.Equal),
                        "not" => FilterByProperty(query, item.Field, value, FilterCheck.NotEqual),
                        "isAnyOf" => FilterByProperty(query, item.Field, value, FilterCheck.IsAnyOf),
                        _ => query
                    };
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    query = item.Operator switch
                    {
                        "is" => FilterByProperty(query, item.Field, item.Value.GetBoolean(), FilterCheck.Equal),
                        "not" => FilterByProperty(query, item.Field, item.Value.GetBoolean(), FilterCheck.NotEqual),
                        _ => query
                    };
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return query;
    }

    public static IEnumerable<T> ApplyDataGridFilter<T>(this IEnumerable<T> source, FilterModel filterModel,
        FilterConfig? filterConfig) => Filter(source, filterModel, filterConfig);

    public static IQueryable<T> ApplyDataGridFilter<T>(this IQueryable<T> source, FilterModel filterModel,
        FilterConfig? filterConfig) => Filter(source, filterModel, filterConfig);
}