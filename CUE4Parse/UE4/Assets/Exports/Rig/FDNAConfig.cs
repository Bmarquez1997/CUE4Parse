using CUE4Parse.UE4;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.Engine;

namespace CUE4Parse.UE4.Assets.Exports.Rig;

public enum EDirection : byte
{
    Left,
    Right,
    Up,
    Down,
    Front,
    Back
}

public enum EDNADataLayer : int
{
    None = 0,
    Descriptor = 1,
    Definition = 2 | Descriptor,
    Behavior = 4 | Definition,
    Geometry = 8 | Definition,
    GeometryWithoutBlendShapes = 16 | Definition,
    MachineLearnedBehavior = 32 | Definition,
    RBFBehavior = 64 | Behavior,
    All = RBFBehavior | Geometry | MachineLearnedBehavior
}

public enum ECoordinateSystemTransformPolicy : byte
{
    Preserve,
    Transform
}

[StructFallback]
public class FRotationSign : IUStruct
{
    [UProperty] public ERotationDirection XAxis;
    [UProperty] public ERotationDirection YAxis;
    [UProperty] public ERotationDirection ZAxis;
}

[StructFallback]
public class FCoordinateSystem : IUStruct
{
    [UProperty] public EDirection XAxis;
    [UProperty] public EDirection YAxis;
    [UProperty] public EDirection ZAxis;
}

[StructFallback]
public class FDNAConfig : IUStruct
{
    [UProperty] public int Layers = (int) EDNADataLayer.All;
    [UProperty] public FPerPlatformInt MaxLODPerPlatform;
    [UProperty] public FPerPlatformInt MinLODPerPlatform;
    [UProperty] public byte[] ExactLODs = [];
    [UProperty] public ECoordinateSystemTransformPolicy CoordinateSystemTransformPolicy = ECoordinateSystemTransformPolicy.Preserve;
    [UProperty] public FCoordinateSystem CoordinateSystem = new();
    [UProperty] public FRotationSign RotationSign = new();
    [UProperty] public ERotationSequence RotationSequence = ERotationSequence.XYZ;
    [UProperty] public EFaceWindingOrder FaceWindingOrder = EFaceWindingOrder.CW;
}

public class UDNAConfigHolder : UObject
{
    [UProperty] public FDNAConfig Config;
}
