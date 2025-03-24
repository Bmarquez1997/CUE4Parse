using System;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.Component.Lights;

public abstract class ULightComponentBase : USceneComponent
{
    [UProperty] public FGuid LightGuid;
    [UProperty] public float Intensity;
    [UProperty] public FColor LightColor;
    [UProperty] public bool CastShadows;
    [UProperty] public bool CastStaticShadows;
    [UProperty] public bool CastDynamicShadows;
    [UProperty] public bool IndirectLightingIntensity;
    [UProperty] public bool VolumetricScatteringIntensity;
}

public class ULightComponent : ULightComponentBase
{
    [UProperty] public ELightUnits IntensityUnits;
    [UProperty] public float Temperature;
    [UProperty] public float MaxDrawDistance;
    [UProperty] public float MaxDistanceFadeRange;
    [UProperty] public bool bUseTemperature;
    [UProperty] public float SpecularScale;
    [UProperty] public float ShadowResolutionScale;
    [UProperty] public float ShadowBias;
    [UProperty] public float ShadowSlopeBias;
    [UProperty] public float ShadowSharpen;
    [UProperty] public FPackageIndex IESTexture;

    public override void Deserialize(FAssetArchive Ar, long validPos) 
    {
        base.Deserialize(Ar, validPos);
        //Intensity = GetOrDefault<float>(nameof(Intensity), 1.0f);
    }

    public virtual double GetNitIntensity() => Intensity;

    protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
    {
        base.WriteJson(writer, serializer);

        var defaultRotation = this is URectLightComponent ? FRotator.ZeroRotator : new FRotator(-90, 0, 0);

        writer.WritePropertyName("RelativeRotation");
        serializer.Serialize(writer, GetOrDefault<FRotator>("RelativeRotation", defaultRotation));

        writer.WritePropertyName("RelativeLocation");
        serializer.Serialize(writer, GetRelativeLocation());

#if DEBUG
        writer.WritePropertyName("RelativeRotationQuat");
        serializer.Serialize(writer, GetOrDefault<FRotator>("RelativeRotation", FRotator.ZeroRotator)
            .GetNormalized().Quaternion());
#endif
    }
}

public class ULocalLightComponent : ULightComponent
{
    [UProperty] public float InverseExposureBlend;
    [UProperty] public float AttenuationRadius;
    [UProperty] public ELightUnits IntensityUnits;

    public override void Deserialize(FAssetArchive Ar, long validPos) 
    {
        base.Deserialize(Ar, validPos);
        //IntensityUnits = GetOrDefault(nameof(IntensityUnits), Owner.Provider.DefaultLightUnit);
    }

    public override double GetNitIntensity() 
    {
        return Intensity;
    }

    protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer) 
    {
        base.WriteJson(writer, serializer);
        writer.WritePropertyName("IntensityNits");
        serializer.Serialize(writer, GetNitIntensity());
    }
}

public class USpotLightComponent : UPointLightComponent 
{
    public float InnerConeAngle { get; private set; }
    public float OuterConeAngle { get; private set; }

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);
        InnerConeAngle = GetOrDefault(nameof(InnerConeAngle), 0.0f);
        OuterConeAngle = GetOrDefault(nameof(OuterConeAngle), 44.0f);
    }

    private float GetHalfConeAngle() 
    {
        var clampedInnerConeAngle = Math.Clamp(InnerConeAngle, 0.0f, 89.0f) * MathF.PI / 180.0f;
        var clampedOuterConeAngle = Math.Clamp(OuterConeAngle * MathF.PI / 180.0f, clampedInnerConeAngle + 0.001f,
            89.0f * MathF.PI / 180.0f + 0.001f);
        return clampedOuterConeAngle;
    }

    public float GetCosHalfConeAngle() 
    {
        return MathF.Cos(GetHalfConeAngle());
    }
}

public class UPointLightComponent : ULocalLightComponent 
{
    public float SourceRadius { get; private set; }
    public bool bUseInverseSquaredFalloff { get; private set; }

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);
        SourceRadius = GetOrDefault(nameof(SourceRadius), 0f);
        bUseInverseSquaredFalloff = GetOrDefault(nameof(bUseInverseSquaredFalloff), true);

        if (!bUseInverseSquaredFalloff) 
            IntensityUnits = ELightUnits.Unitless;
    }

    public override double GetNitIntensity() 
    {
        if (!bUseInverseSquaredFalloff)
            return Intensity; // Unitless brightness
        

        double solidAngle = 4f * Math.PI;
        if (this is USpotLightComponent spotLightComponent) 
            solidAngle = 2f * Math.PI * (1.0f - spotLightComponent.GetCosHalfConeAngle());

        float areaInSqMeters =
            (float)Math.Max(solidAngle * Math.Pow(SourceRadius / 100f, 2), UnrealMath.KindaSmallNumber);

        return LightUtils.ConvertToIntensityToNits(Intensity, areaInSqMeters, solidAngle, IntensityUnits);
    }

    protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
    {
        base.WriteJson(writer, serializer);
        writer.WritePropertyName("bUseInverseSquaredFalloff");
        writer.WriteValue(bUseInverseSquaredFalloff);
    }
}

public class URectLightComponent : ULocalLightComponent 
{
    public float SourceWidth;
    public float SourceHeight;

    public override void Deserialize(FAssetArchive Ar, long validPos) 
    {
        base.Deserialize(Ar, validPos);
        SourceWidth = GetOrDefault(nameof(SourceWidth), 64.0f);
        SourceHeight = GetOrDefault(nameof(SourceHeight), 64.0f);
    }

    public override double GetNitIntensity() 
    {
        var areaInSqMeters = (SourceWidth / 100.0f) * (SourceHeight / 100.0f);
        const double angle = Math.PI;
        return LightUtils.ConvertToIntensityToNits(Intensity, areaInSqMeters, angle, IntensityUnits);
    }
}

public class UDirectionalLightComponent : ULightComponent 
{
    [UProperty] public float ShadowCascadeBiasDistribution;
    [UProperty] public uint bEnableLightShaftOcclusion = 1;
    [UProperty] public float OcclusionMaskDarkness;
    [UProperty] public float OcclusionDepthRange;
    [UProperty] public FVector LightShaftOverrideDirection;
    [UProperty] public float WholeSceneDynamicShadowRadius_DEPRECATED;
    [UProperty] public float DynamicShadowDistanceMovableLight;
    [UProperty] public float DynamicShadowDistanceStationaryLight;
    [UProperty] public int DynamicShadowCascades;
    [UProperty] public float CascadeDistributionExponent;
    [UProperty] public float CascadeTransitionFraction;
    [UProperty] public float ShadowDistanceFadeoutFraction;
    [UProperty] public uint bUseInsetShadowsForMovableObjects = 1;
    [UProperty] public int FarShadowCascadeCount;
    [UProperty] public float FarShadowDistance;
    [UProperty] public float DistanceFieldShadowDistance;
    [UProperty] public int ForwardShadingPriority;
    [UProperty] public float LightSourceAngle;
    [UProperty] public float LightSourceSoftAngle;
    [UProperty] public float ShadowSourceAngleFactor;
    [UProperty] public float TraceDistance;
    [UProperty] public uint bUsedAsAtmosphereSunLight_DEPRECATED = 1;
    [UProperty] public uint bAtmosphereSunLight  = 1;
    [UProperty] public int AtmosphereSunLightIndex;
    [UProperty] public FLinearColor AtmosphereSunDiskColorScale;
    [UProperty] public uint bPerPixelAtmosphereTransmittance = 1;
    [UProperty] public uint bCastShadowsOnClouds = 1;
    [UProperty] public uint bCastShadowsOnAtmosphere = 1;
    [UProperty] public uint bCastCloudShadows = 1;
    [UProperty] public float CloudShadowStrength;
    [UProperty] public float CloudShadowOnAtmosphereStrength;
    [UProperty] public float CloudShadowOnSurfaceStrength;
    [UProperty] public float CloudShadowDepthBias;
    [UProperty] public float CloudShadowExtent;
    [UProperty] public float CloudShadowMapResolutionScale;
    [UProperty] public float CloudShadowRaySampleCountScale;
    [UProperty] public FLinearColor CloudScatteredLuminanceScale;
    [UProperty] public uint bCastModulatedShadows = 1;
    [UProperty] public FColor ModulatedShadowColor;
    [UProperty] public float ShadowAmount;

    // public override double GetNitIntensity() 
    // {
    //     return LightUtils.ConvertToIntensityToNits(Intensity, areaInSqMeters, solidAngle, IntensityUnits);
    // }

    protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
    {
        base.WriteJson(writer, serializer);
    }
}