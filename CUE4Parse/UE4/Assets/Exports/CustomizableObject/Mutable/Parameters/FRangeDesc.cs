using CUE4Parse.UE4.Assets.Readers;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Parameters;

public class FRangeDesc
{
    public string Name;
    public string UID;
    public int DimensionParameter;

    public FRangeDesc(FMutableArchive Ar)
    {
        Name = Ar.ReadMutableFString();
        UID = Ar.ReadMutableFString();
        DimensionParameter = Ar.Read<int>();
    }
}