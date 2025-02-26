using CUE4Parse.UE4.Readers;
using Newtonsoft.Json;
using FIntVector2 = CUE4Parse.UE4.Objects.Core.Math.TIntVector2<int>;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Layout;

[JsonConverter(typeof(FLayoutConverter))]
public class FLayout
{
    public FIntVector2 Size;
    public FIntVector2 MaxSize;
    public FLayoutBlock[] Blocks;
    public EPackStrategy Strategy;
    public EReductionMethod ReductionMethod;

    public FLayout(FArchive Ar)
    {
        Size = Ar.Read<FIntVector2>();
        Blocks = Ar.ReadArray(() => new FLayoutBlock(Ar));
        MaxSize = Ar.Read<FIntVector2>();
        Strategy = Ar.Read<EPackStrategy>();
        ReductionMethod = Ar.Read<EReductionMethod>();
    }
}

public enum EPackStrategy : uint
{
    Resizeable,
    Fixed,
    Overlay
}

public enum EReductionMethod : uint
{
    Halve,	// Divide axis by 2
    Unitary	// Reduces 1 block the axis
}
