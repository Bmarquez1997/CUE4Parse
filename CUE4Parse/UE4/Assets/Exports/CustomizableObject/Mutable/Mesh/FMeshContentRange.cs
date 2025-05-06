using System;
using CUE4Parse.UE4.Assets.Readers;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh;

public class FMeshContentRange
{
    public uint MeshIdPrefix;
    public EMeshContentFlags ContentFlags;
    public uint FirstIndex;

    private static int FirstIndexMaxBits   = 24;
    private static int ContentFlagsMaxBits = 32 - FirstIndexMaxBits;
    private static int FirstIndexBitMask   = (1 << FirstIndexMaxBits) - 1;
    public FMeshContentRange(FMutableArchive Ar)
    {
        var firstIndex_ContentFlags = Ar.Read<uint>();

        MeshIdPrefix = Ar.Read<uint>();
        ContentFlags = (EMeshContentFlags)((firstIndex_ContentFlags >> FirstIndexMaxBits) & ((1 << ContentFlagsMaxBits) - 1));
        FirstIndex = (uint)(firstIndex_ContentFlags & FirstIndexBitMask);
    }
}

[Flags]
public enum EMeshContentFlags : byte
{
    None          = 0,
    GeometryData  = 1 << 0,
    PoseData      = 1 << 1,
    PhysicsData   = 1 << 2,
    MetaData      = 1 << 3,

    LastFlag      = MetaData,
    AllFlags      = (LastFlag << 1) - 1,
}
