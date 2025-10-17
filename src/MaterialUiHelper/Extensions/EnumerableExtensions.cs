using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using MaterialUIHelper.Enums;
using MaterialUIHelper.Models.filter;
using MaterialUIHelper.Services;

namespace MaterialUIHelper.Extensions;

public static class EnumerableExtensions
{
    private static IMaterialUiHelperContext? Context { get; set; }

    internal static void Initialize(IMaterialUiHelperContext context)
    {
        Context = context;
    }

    private static IQueryable<T> ApplyFilterQueryable<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate)
    {
        return source.Where(predicate);
    }

    private static IEnumerable<T> ApplyFilterEnumerable<T>(IEnumerable<T> source, Func<T, bool> predicate)
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
        var genericArgs = source.GetType().GetGenericArguments();
        Type elementType = genericArgs.Length > 0
            ? genericArgs[0]
            : typeof(TItem);

        var parameter = Expression.Parameter(elementType, "item");
        var propertyInfo = elementType.GetProperties()
            .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        if (propertyInfo is null)
        {
            throw new ArgumentException(
                $"Property '{propertyName}' does not exist on type '{typeof(TItem).Name}'.");
        }

        var property = Expression.Property(parameter, propertyInfo);

        Expression predicate;
        Expression constant = Expression.Constant(value, typeof(TValue));

        // Create a null-check expression
        var nullConstant = Expression.Constant(null, typeof(TValue));
        var notNullProperty = Expression.NotEqual(property, nullConstant);
        // Make sure the constant is cast to the same type as the property (Nullable<DateTime>)
        var castedConstant =
            property.Type.IsGenericType && property.Type.GetGenericTypeDefinition() == typeof(Nullable<>)
                ? Expression.Convert(constant, property.Type)
                : constant;

        if (Context is not null && Context.Options.Filters.TryGetValue(check, out var customBuilder))
        {
            predicate = customBuilder(property, castedConstant);
        }
        else
        {
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
                predicate = check switch
                {
                    FilterCheck.Equal => Expression.AndAlso(notNullProperty,
                        Expression.Equal(property, castedConstant)),
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
                        Expression.NotEqual(property,
                            Expression.Constant(null, property.Type)), // Check for not null
                        Expression.NotEqual(property, Expression.Constant("")) // Check for not empty string
                    ),
                    // FilterCheck.IsAnyOf => Expression.Call(typeof(Enumerable).GetMethods()
                    //         .First(p => p.Name == nameof(Enumerable.Contains) && p.GetParameters().Length == 2)
                    //         .MakeGenericMethod(typeof(TValue)),
                    //     Expression.Constant(value, typeof(IEnumerable<TValue>)),
                    //     property),
                    _ => throw new ArgumentOutOfRangeException(nameof(check), "Invalid filter check")
                };
            }
        }


        // var lambda = Expression.Lambda<Func<TItem, bool>>(predicate, parameter);
        var lambdaType = typeof(Func<,>).MakeGenericType(elementType, typeof(bool));
        var lambda = Expression.Lambda(lambdaType, predicate, parameter);
        var method = source is IQueryable
            ? typeof(EnumerableExtensions).GetMethod(nameof(ApplyFilterQueryable),
                BindingFlags.NonPublic | BindingFlags.Static)
            : typeof(EnumerableExtensions).GetMethod(nameof(ApplyFilterEnumerable),
                BindingFlags.NonPublic | BindingFlags.Static);
        var genericMethod = method!.MakeGenericMethod(elementType);

        switch (source)
        {
            case IQueryable:
                return genericMethod.Invoke(null, new[] { source, lambda });
            case IEnumerable:
            {
                var compiled = lambda.Compile();

                return genericMethod.Invoke(null, new[] { source, compiled });
            }
            default:
                throw new ArgumentException(
                    $"The source type {source.GetType()} is not supported. Supported types include IQueryable<T> or IEnumerable<T>.");
        }
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

    private static object Filter(dynamic query, FilterModel model, FilterConfig? config = null)
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
        FilterConfig? filterConfig = null) => (IEnumerable<T>)Filter(source, filterModel, filterConfig);

    public static IQueryable<T> ApplyDataGridFilter<T>(this IQueryable<T> source, FilterModel filterModel,
        FilterConfig? filterConfig = null) => (IQueryable<T>)Filter(source, filterModel, filterConfig);
}