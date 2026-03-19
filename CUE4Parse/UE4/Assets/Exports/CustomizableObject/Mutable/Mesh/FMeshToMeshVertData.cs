using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Math;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh;

/// <summary>
/// Matches C++ FMeshToMeshVertData serialization (Serialisation.cpp): PositionBaryCoordsAndDist, NormalBaryCoordsAndDist,
/// TangentBaryCoordsAndDist, SourceMeshVertIndices[0..3], Weight. Padding is not serialized.
/// </summary>
public struct FMeshToMeshVertData
{
    public FVector4 PositionBaryCoordsAndDist;
    public FVector4 NormalBaryCoordsAndDist;
    public FVector4 TangentBaryCoordsAndDist;
    public ushort SourceMeshVertIndex0;
    public ushort SourceMeshVertIndex1;
    public ushort SourceMeshVertIndex2;
    public ushort SourceMeshVertIndex3;
    public float Weight;

    public FMeshToMeshVertData(FMutableArchive Ar)
    {
        PositionBaryCoordsAndDist = Ar.Read<FVector4>();
        NormalBaryCoordsAndDist = Ar.Read<FVector4>();
        TangentBaryCoordsAndDist = Ar.Read<FVector4>();
        SourceMeshVertIndex0 = Ar.Read<ushort>();
        SourceMeshVertIndex1 = Ar.Read<ushort>();
        SourceMeshVertIndex2 = Ar.Read<ushort>();
        SourceMeshVertIndex3 = Ar.Read<ushort>();
        Weight = Ar.Read<float>();
    }
}