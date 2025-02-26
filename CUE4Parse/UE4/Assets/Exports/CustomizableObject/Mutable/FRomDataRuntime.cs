using CUE4Parse.UE4.Readers;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;

[JsonConverter(typeof(FRomDataRuntimeConverter))]
public class FRomDataRuntime
{
    public uint Size;
    public ERomDataType ResourceType;
    public bool IsHighRes;

    public FRomDataRuntime(FArchive Ar)
    {
        var packedValue = Ar.Read<uint>();

        Size = packedValue & (1 << 30) - 1;
        ResourceType = (ERomDataType) ((packedValue >> 30) & 1);
        IsHighRes = ((packedValue >> 31) & 1) != 0;
    }
}

public enum ERomDataType : uint
{
    Image = 0,
    Mesh  = 1
}
