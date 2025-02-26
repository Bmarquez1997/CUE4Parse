using CUE4Parse.UE4.Readers;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;

public class FConstantResourceIndex
{
    public uint Index;
    public bool Streamable;

    public FConstantResourceIndex(FArchive Ar)
    {
        var packedValue = Ar.Read<uint>();

        Index = packedValue & (1U << 31) - 1;
        Streamable = ((packedValue >> 31) & 1) != 0;
    }
}
