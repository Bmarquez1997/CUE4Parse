using CUE4Parse.UE4;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.Engine;

namespace CUE4Parse.UE4.Assets.Exports.Rig;

public class FRigLogic
{
    public FRigLogicConfiguration Configuration;
}

[StructFallback]
public class FRigLogicConfiguration : IUStruct
{
    [UProperty] public FPerPlatformERigLogicCalculationType CalculationTypePerPlatform;
    [UProperty] public FPerPlatformERigLogicFloatingPointType FloatingPointTypePerPlatform;
    [UProperty] public FPerPlatformBool EnableMultiThreadMLComputePerPlatform;
    [UProperty] public bool LoadJoints = true;
    [UProperty] public bool LoadBlendShapes = true;
    [UProperty] public bool LoadAnimatedMaps = true;
    [UProperty] public bool LoadMachineLearnedBehavior = true;
    [UProperty] public bool LoadRBFBehavior = true;
    [UProperty] public bool LoadTwistSwingBehavior = true;
    [UProperty] public ERigLogicTranslationType TranslationType = ERigLogicTranslationType.Vector;
    [UProperty] public ERigLogicRotationType RotationType = ERigLogicRotationType.EulerAngles;
    [UProperty] public ERigLogicScaleType ScaleType = ERigLogicScaleType.Vector;
    [UProperty] public float TranslationPruningThreshold;
    [UProperty] public float RotationPruningThreshold;
    [UProperty] public float ScalePruningThreshold;
}

public enum ERigLogicTranslationType : uint
{
    None,
    Vector = 3
}

public enum ERigLogicRotationType : uint
{
    None,
    EulerAngles = 3,
    Quaternions = 4
}

public enum ERigLogicScaleType : uint
{
    None,
    Vector = 3
}
