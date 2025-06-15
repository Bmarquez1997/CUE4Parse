using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.UObject;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject;

public class UModelResources : UObject
{
    public FSoftObjectPath[] Skeletons;
    public FSoftObjectPath[] Materials;
    public FSoftObjectPath[] PhysicsAssets;
    public UScriptMap? BoneNamesMap;
    public UScriptMap? MeshMetadata;
    public UScriptMap? SurfaceMetadata;

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);

        Skeletons = GetOrDefault<FSoftObjectPath[]>(nameof(Skeletons), []);
        Materials = GetOrDefault<FSoftObjectPath[]>(nameof(Materials), []);
        PhysicsAssets = GetOrDefault<FSoftObjectPath[]>(nameof(PhysicsAssets), []);
        BoneNamesMap = GetOrDefault<UScriptMap>(nameof(BoneNamesMap));
        MeshMetadata = GetOrDefault<UScriptMap>(nameof(MeshMetadata));
        SurfaceMetadata = GetOrDefault<UScriptMap>(nameof(SurfaceMetadata));
    }
}
