using MaterialUIHelper.Extensions;
using MaterialUIHelper.Options;

namespace MaterialUIHelper.Services;

public class MaterialUiHelperService : IMaterialUiHelperService
{
    public MaterialUiHelperOptions Options { get; }

    public MaterialUiHelperService(MaterialUiHelperOptions options, IMaterialUiHelperContext context)
    {
        Options = options;

        EnumerableExtensions.Initialize(context);
    }
}