using System;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Image;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Layout;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Parameters;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Physics;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Skeleton;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine.Curves;
using CUE4Parse.UE4.Readers;
using Newtonsoft.Json;
using Serilog;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;

[JsonConverter(typeof(FProgramConverter))]
public class FProgram
{
    public uint[] OpAddress;
    public byte[] ByteCode;
    public FState[] States;
    public FRomDataRuntime[] Roms;
    public FRomDataCompile[] RomsCompileData;
    public FImage[] ConstantImageLODsPermanent;
    public FConstantResourceIndex[] ConstantImageLODIndices;
    public FImageLODRange[] ConstantImages;
    public FMesh[] ConstantMeshesPermanent;
    public FExtensionDataConstant[] ConstantExtensionData;
    public string[] ConstantStrings;
    public FLayout[] ConstantLayouts;
    public FProjector[] ConstantProjectors;
    public FMatrix[] ConstantMatrices;
    public FShape[] ConstantShapes;
    public FRichCurve[] ConstantCurves;
    public FSkeleton[] ConstantSkeletons;
    public FPhysicsBody[] ConstantPhysicsBodies;
    public FParameterDesc[] Parameters;
    public FRangeDesc[] Ranges;
    public ushort[][] ParameterLists;

    public FProgram(FMutableArchive Ar)
    {
        OpAddress = Ar.ReadArray<uint>();
        ByteCode = Ar.ReadArray<byte>();
        States = Ar.ReadArray(() => new FState(Ar));
        Roms = Ar.ReadArray(() => new FRomDataRuntime(Ar));
        RomsCompileData = Ar.ReadArray(() => new FRomDataCompile(Ar));
        try
        {
            ConstantImageLODsPermanent = Ar.ReadPtrArray(() => new FImage(Ar));
            ConstantImageLODIndices = Ar.ReadArray(() => new FConstantResourceIndex(Ar));
            ConstantImages = Ar.ReadArray(() => new FImageLODRange(Ar));
            ConstantMeshesPermanent = Ar.ReadPtrArray(() => new FMesh(Ar));
            ConstantExtensionData = Ar.ReadArray(() => new FExtensionDataConstant(Ar));
            ConstantStrings = Ar.ReadArray(Ar.ReadFString);
            ConstantLayouts = Ar.ReadPtrArray(() => new FLayout(Ar));
            ConstantProjectors = Ar.ReadArray(() => new FProjector(Ar));
            ConstantMatrices = Ar.ReadArray(() => new FMatrix(Ar, false));
            ConstantShapes = Ar.ReadArray(() => new FShape(Ar));
            ConstantCurves = Ar.ReadArray(() => new FRichCurve(Ar));
            ConstantSkeletons = Ar.ReadPtrArray(() => new FSkeleton(Ar));
            ConstantPhysicsBodies = Ar.ReadPtrArray(() => new FPhysicsBody(Ar));
            Parameters = Ar.ReadArray(() => new FParameterDesc(Ar));
            Ranges = Ar.ReadArray(() => new FRangeDesc(Ar));
            ParameterLists = Ar.ReadArray(Ar.ReadArray<ushort>);
        }
        catch (Exception e)
        {
            Log.Error("Exception thrown while loading FProgram: {}", e);
        }
        
    }
}
