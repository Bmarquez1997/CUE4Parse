using CUE4Parse.UE4.Assets.Readers;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh;

/// <summary>
/// Matches C++ FCloth serialization: Data (TArray of FMeshToMeshVertData) only. ClothingAsset is not serialized.
/// </summary>
public class FCloth
{
    public FMeshToMeshVertData[] Data;

    public FCloth(FMutableArchive Ar)
    {
        Data = Ar.ReadArray(() => new FMeshToMeshVertData(Ar));
    }
}