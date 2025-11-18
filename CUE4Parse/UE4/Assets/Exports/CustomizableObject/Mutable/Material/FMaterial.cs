using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Image;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Material;

public class FMaterial
{
    public int ReferenceID;
    public Dictionary<FName, TVariant<uint, FImage?>?> ImageParameters;
    public Dictionary<FName, FVector4> ColorParameters;
    public Dictionary<FName, float> ScalarParameters;
    
    
    public FMaterial(FMutableArchive Ar)
    {
        ReferenceID = Ar.Read<int>();
        ImageParameters = Ar.ReadMap(() => (Ar.ReadFName(), Ar.ReadTVariant(Ar.Read<uint>, () => Ar.ReadPtr(() => new FImage(Ar)))));
        ColorParameters = Ar.ReadMap(() => (Ar.ReadFName(), Ar.Read<FVector4>()));
        ScalarParameters = Ar.ReadMap(() => (Ar.ReadFName(), Ar.Read<float>()));
    }
}