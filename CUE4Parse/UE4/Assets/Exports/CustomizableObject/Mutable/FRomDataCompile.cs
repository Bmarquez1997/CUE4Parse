using CUE4Parse.UE4.Readers;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;

public class FRomDataCompile
{
    public uint SourceId;

    public FRomDataCompile(FArchive Ar)
    {
        SourceId = Ar.Read<uint>();
    }
}
