﻿using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Readers;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Physics.Bodies;

public class FBoxBody : FBodyShape
{
    public FVector Position;
    public FQuat Orientation;
    public FVector Size;

    public FBoxBody(FMutableArchive Ar) : base(Ar)
    {
        Position = Ar.Read<FVector>();
        Orientation = Ar.Read<FQuat>();
        Size = Ar.Read<FVector>();
    }
}
