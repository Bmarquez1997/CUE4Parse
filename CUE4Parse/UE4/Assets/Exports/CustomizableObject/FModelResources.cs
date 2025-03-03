using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.UObject;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject;

[StructFallback]
public class FModelResources
{
    public FSoftObjectPath[] Skeletons;
    public FSoftObjectPath[] Materials;
    public UScriptMap? BoneNamesMap;

    public FModelResources(FStructFallback fallback)
    {
        Skeletons = fallback.GetOrDefault<FSoftObjectPath[]>(nameof(Skeletons), []);
        Materials = fallback.GetOrDefault<FSoftObjectPath[]>(nameof(Materials), []);
        BoneNamesMap = fallback.GetOrDefault<UScriptMap>(nameof(BoneNamesMap));
    }
}
