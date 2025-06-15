using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.UObject;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject;

public class UCustomizableObjectPrivate : UObject
{
    public FPackageIndex ModelStreamableData;
    public FPackageIndex ModelResources;

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);

        ModelStreamableData = GetOrDefault<FPackageIndex>(nameof(ModelStreamableData));
        ModelResources = GetOrDefault<FPackageIndex>(nameof(ModelResources));
    }
}
