using CUE4Parse.UE4.Assets.Readers;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Roms;

public class FRomDataRuntime
{
    public uint Size;
    public ERomDataType ResourceType;
    public bool bIsHighRes;

    public FRomDataRuntime(FMutableArchive Ar)
    {
        var bitField = Ar.Read<uint>();

        Size = bitField & 0x3FFFFFFF;
        ResourceType = (ERomDataType)((bitField & 0x40000000) >> 30);
        bIsHighRes = (bitField & 0x80000000) >> 31 != 0;
    }
}

public enum ERomDataType : uint
{
    Image = 0,
    Mesh  = 1
}
