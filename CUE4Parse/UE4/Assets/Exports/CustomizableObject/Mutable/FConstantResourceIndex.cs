using CUE4Parse.UE4.Assets.Readers;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;

public class FConstantResourceIndex
{
    public uint Index;
    public bool Streamable;

    public FConstantResourceIndex(FMutableArchive Ar)
    {
        var bitfield = Ar.Read<uint>();

        Index = bitfield & 0x7FFFFFFF;
        Streamable = (bitfield & 0x80000000) >> 31 != 0;
    }
}
