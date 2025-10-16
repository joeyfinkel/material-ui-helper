using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using MaterialUIHelper.Enums;

namespace MaterialUIHelper.Options;

public class MaterialUiHelperOptions
{
    public Dictionary<FilterCheck, Func<Expression, Expression, Expression>> Filters { get; } = new();

    public void RegisterFilter(FilterCheck filter, Func<Expression, Expression, Expression> filterExpression)
    {
        Filters[filter] = filterExpression;
    }
}