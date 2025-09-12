using System;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Parameters;

public class FParameterDesc
{
    public string Name;
    public FGuid UID;
    public EParameterType Type;
    public object? DefaultValue;
    public uint[] Ranges;
    public FIntValueDesc[] PossibleValues;

    public FParameterDesc(FMutableArchive Ar)
    {
        Name = Ar.ReadFString();
        UID = Ar.Read<FGuid>();
        Type = Ar.Read<EParameterType>();

        Ar.Position += 1;
        DefaultValue = Type switch
        {
            EParameterType.None or EParameterType.Image => null,
            EParameterType.Bool => Ar.ReadBoolean(),
            EParameterType.Int => Ar.Read<int>(),
            EParameterType.Float => Ar.Read<float>(),
            EParameterType.Color => Ar.Read<FVector4>(),
            EParameterType.Projector => Ar.Read<FProjector>(),
            EParameterType.String => Ar.ReadString(),
            EParameterType.Matrix => new FMatrix(Ar, false),
            _ => throw new NotSupportedException($"Type {Type} is currently not supported")
        };

        Ranges = Ar.ReadArray<uint>();
        PossibleValues = Ar.ReadArray(() => new FIntValueDesc(Ar));
    }
}

public enum EParameterType : uint
{
    /** Undefined parameter type. */
    None,

    /** Boolean parameter type (true or false) */
    Bool,

    /** Integer parameter type. It usually has a limited range of possible values that can be queried in the FParameters object. */
    Int,

    /** Floating point value in the range of 0.0 to 1.0 */
    Float,

    /** Floating point RGBA colour, with each channel ranging from 0.0 to 1.0 */
    Color,

    /** 3D Projector type, defining a position, scale and orientation.Basically used for projected decals. */
    Projector,

    /** An externally provided image. */
    Image,

    /** An externally provided mesh. */
    Mesh,

    /** A text string. */
    String,

    /** A 4x4 matrix. */
    Matrix,

    /** Utility enumeration value, not really a parameter type. */
    Count
}
