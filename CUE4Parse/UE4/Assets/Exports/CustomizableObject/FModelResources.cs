﻿using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.UObject;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject;

[StructFallback]
public class FModelResources
{
    public FSoftObjectPath[] Skeletons;
    public FSoftObjectPath[] Materials;
    public FSoftObjectPath[] PhysicsAssets;
    public UScriptMap? BoneNamesMap;
    public UScriptMap? MeshMetadata;
    public UScriptMap? SurfaceMetadata;

    public FModelResources(FStructFallback fallback)
    {
        Skeletons = fallback.GetOrDefault<FSoftObjectPath[]>(nameof(Skeletons), []);
        Materials = fallback.GetOrDefault<FSoftObjectPath[]>(nameof(Materials), []);
        PhysicsAssets = fallback.GetOrDefault<FSoftObjectPath[]>(nameof(PhysicsAssets), []);
        BoneNamesMap = fallback.GetOrDefault<UScriptMap>(nameof(BoneNamesMap));
        MeshMetadata = fallback.GetOrDefault<UScriptMap>(nameof(MeshMetadata));
        SurfaceMetadata = fallback.GetOrDefault<UScriptMap>(nameof(SurfaceMetadata));
    }
}
