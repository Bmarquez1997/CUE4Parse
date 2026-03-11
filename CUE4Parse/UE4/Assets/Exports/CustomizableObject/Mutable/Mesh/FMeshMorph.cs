using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Math;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh;

/// <summary>
/// Matches C++ FMeshMorph serialization order: Names, MaximumValuePerMorph, MinimumValuePerMorph,
/// BatchStartOffsetPerMorph, BatchesPerMorph, SurfacesInUsePerMorph, UsageFlagsPerMorph,
/// NumTotalBatches, PositionPrecision, TangentZPrecision.
/// </summary>
public class FMeshMorph
{
    public string[] Names;
    public FVector4[] MaximumValuePerMorph;
    public FVector4[] MinimumValuePerMorph;
    public uint[] BatchStartOffsetPerMorph;
    public uint[] BatchesPerMorph;
    public int[][] SurfacesInUsePerMorph;
    public EMorphUsageFlags[] UsageFlagsPerMorph;
    public uint NumTotalBatches;
    public float PositionPrecision;
    public float TangentZPrecision;

    public FMeshMorph(FMutableArchive Ar)
    {
        Names = Ar.ReadArray(Ar.ReadMutableFString);
        MaximumValuePerMorph = Ar.ReadArray<FVector4>();
        MinimumValuePerMorph = Ar.ReadArray<FVector4>();
        BatchStartOffsetPerMorph = Ar.ReadArray<uint>();
        BatchesPerMorph = Ar.ReadArray<uint>();
        SurfacesInUsePerMorph = Ar.ReadArray(Ar.ReadArray<int>);
        UsageFlagsPerMorph = Ar.ReadArray<EMorphUsageFlags>();
        NumTotalBatches = Ar.Read<uint>();
        PositionPrecision = Ar.Read<float>();
        TangentZPrecision = Ar.Read<float>();
    }
}

public enum EMorphUsageFlags : byte
{
    None = 0,
    Baked = 1 << 0,
    RealTime = 1 << 1,
    External = 1 << 2,
}