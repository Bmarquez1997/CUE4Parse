using CUE4Parse.UE4.Readers;
using FIntVector2 = CUE4Parse.UE4.Objects.Core.Math.TIntVector2<int>;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Layout;

public class FLayoutBlock
{
    public FIntVector2 Min;
    public FIntVector2 Size;
    public ulong Id;
    public int Priority;
    public bool bReduceBothAxes;
    public bool bReduceByTwo;
    public uint UnusedPadding;

    public FLayoutBlock(FArchive Ar)
    {
        Min = Ar.Read<FIntVector2>();
        Size = Ar.Read<FIntVector2>();
        Id = Ar.Read<ulong>();
        Priority = Ar.Read<int>();

        var packedValue = Ar.Read<uint>();

        bReduceBothAxes = ((packedValue >> 0) & 1) != 0;
        bReduceByTwo = ((packedValue >> 1) & 1) != 0;
        UnusedPadding = (packedValue >> 2) & ((1U << 30) - 1);
    }
}
