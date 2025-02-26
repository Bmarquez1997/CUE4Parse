using CUE4Parse.UE4.Readers;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;

[JsonConverter(typeof(FModelConverter))]
public class FModel
{
    public FProgram Program;

    public FModel(FArchive Ar)
    {
        Program = new FProgram(Ar);
    }
}
