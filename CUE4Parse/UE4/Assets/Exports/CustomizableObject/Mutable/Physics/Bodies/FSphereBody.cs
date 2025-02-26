using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Readers;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Physics.Bodies;

public class FSphereBody : FBodyShape
{
    public FVector Position;
    public float Radius;

    public FSphereBody(FMutableArchive Ar) : base(Ar)
    {
        Position = Ar.Read<FVector>();
        Radius = Ar.Read<float>();
    }
}
