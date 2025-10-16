using System.Collections.Generic;

namespace MaterialUIHelper.Models;

public class BaseConfig
{
    /// <summary>
    /// List of fields that should be ignored.
    /// </summary>
    public List<string> FieldsToIgnore { get; set; } = [];
}