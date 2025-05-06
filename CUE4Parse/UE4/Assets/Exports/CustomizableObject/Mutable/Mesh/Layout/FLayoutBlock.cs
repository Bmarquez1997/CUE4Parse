using CUE4Parse.UE4.Assets.Readers;
using FIntVector2 = CUE4Parse.UE4.Objects.Core.Math.TIntVector2<int>;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh.Layout;

public class FLayoutBlock
{
    public FIntVector2 Min;
    public FIntVector2 Size;
    public ulong Id;
    public int Priority;
    public bool bReduceBothAxes;
    public bool bReduceByTwo;
    public uint UnusedPadding;

    public FLayoutBlock(FMutableArchive Ar)
    {
        Min = Ar.Read<FIntVector2>();
        Size = Ar.Read<FIntVector2>();
        Id = Ar.Read<ulong>();
        Priority = Ar.Read<int>();

        var bitfield = Ar.Read<uint>();
        bReduceBothAxes = (bitfield & 0x1) != 0;
        bReduceByTwo = (bitfield & 0x2) >> 1 != 0;
        UnusedPadding = bitfield & 0xFFFFFFFC;
    }
}
