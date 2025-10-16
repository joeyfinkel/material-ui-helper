using MaterialUIHelper.Options;

namespace MaterialUIHelper.Services;

public interface IMaterialUiHelperContext
{
    MaterialUiHelperOptions Options { get; }
}

internal class MaterialUiHelperContext(MaterialUiHelperOptions options) : IMaterialUiHelperContext
{
    public MaterialUiHelperOptions Options { get; } = options;
}