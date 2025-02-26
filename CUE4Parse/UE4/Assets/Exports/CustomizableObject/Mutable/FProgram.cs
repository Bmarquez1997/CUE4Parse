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

    public FProgram(FArchive Ar)
    {
        var mutableAr = new FMutableArchive(Ar, Ar.Versions);

        OpAddress = Ar.ReadArray<uint>();
        ByteCode = Ar.ReadArray<byte>();
        States = Ar.ReadArray(() => new FState(mutableAr));
        Roms = Ar.ReadArray(() => new FRomDataRuntime(Ar));
        RomsCompileData = Ar.ReadArray(() => new FRomDataCompile(Ar));
        ConstantImageLODsPermanent = mutableAr.ReadPtrArray(() => new FImage(Ar));
        ConstantImageLODIndices = Ar.ReadArray(() => new FConstantResourceIndex(Ar));
        ConstantImages = Ar.ReadArray(() => new FImageLODRange(Ar));
        ConstantMeshesPermanent = mutableAr.ReadPtrArray(() => new FMesh(mutableAr));
        ConstantExtensionData = mutableAr.ReadArray(() => new FExtensionDataConstant(mutableAr));
        ConstantStrings = Ar.ReadArray(mutableAr.ReadFString);
        ConstantLayouts = mutableAr.ReadPtrArray(() => new FLayout(Ar));
        ConstantProjectors = Ar.ReadArray(() => new FProjector(Ar));
        ConstantMatrices = Ar.ReadArray(() => new FMatrix(Ar, false));
        ConstantShapes = Ar.ReadArray(() => new FShape(Ar));
        ConstantCurves = Ar.ReadArray(() => new FRichCurve(Ar));
        ConstantSkeletons = mutableAr.ReadPtrArray(() => new FSkeleton(Ar));
        ConstantPhysicsBodies = mutableAr.ReadPtrArray(() => new FPhysicsBody(mutableAr));
        Parameters = Ar.ReadArray(() => new FParameterDesc(mutableAr));
        Ranges = Ar.ReadArray(() => new FRangeDesc(mutableAr));
        ParameterLists = Ar.ReadArray(Ar.ReadArray<ushort>);
    }
}
