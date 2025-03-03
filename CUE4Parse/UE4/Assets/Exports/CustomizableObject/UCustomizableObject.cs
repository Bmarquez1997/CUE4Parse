using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.UObject;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject;

public class UCustomizableObject : UObject
{
    public FPackageIndex Private;
    public ECustomizableObjectVersion Version;
    public FModel Model;

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);

        Private = GetOrDefault<FPackageIndex>(nameof(Private));

        Version = Ar.Read<ECustomizableObjectVersion>();
        Model = new FModel(Ar);
    }

    protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
    {
        base.WriteJson(writer, serializer);

        writer.WritePropertyName("Version");
        serializer.Serialize(writer, $"ECustomizableObjectVersion::{Version.ToString()}");

        writer.WritePropertyName("Model");
        serializer.Serialize(writer, Model);
    }
}

public enum ECustomizableObjectVersion
{
    FirstEnumeratedVersion = 450,

    DeterminisiticMeshVertexIds,

    NumRunFtimeReferencedTextures,

    DeterminisiticLayoutBlockIds,

    BackoutDeterminisiticLayoutBlockIds,

    FixWrappingProjectorLayoutBlockId,

    MeshReferenceSupport,

    ImproveMemoryUsageForStreamableBlocks,

    FixClipMeshWithMeshCrash,

    SkeletalMeshLODSettingsSupport,

    RemoveCustomCurve,

    AddEditorGamePlayTags,

    AddedParameterThumbnailsToEditor,

    ComponentsLODsRedesign,

    ComponentsLODsRedesign2,

    LayoutToPOD,

    AddedRomFlags,

    LayoutNodeCleanup,

    AddSurfaceAndMeshMetadata,

    TablesPropertyNameBug,

    DataTablesParamTrackingForCompileOnlySelected,

    CompilationOptimizationsMeshFormat,

    ModelStreamableBulkData,

    LayoutBlocksAsInt32,

    IntParameterOptionDataTable,

    RemoveLODCountLimit,

    IntParameterOptionDataTablePartialBackout,

    IntParameterOptionDataTablePartialRestore,

    CorrectlySerializeTableToParamNames,

    AddMaterialSlotNameIndexToSurfaceMetadata,

    NodeComponentMesh,

    MoveEditNodesToModifiers,

    DerivedDataCache,

    ComponentsArray,

    FixComponentNames,

    AddedFaceCullStrategyToSomeOperations,

    DDCParticipatingObjects,

    GroupRomsBySource,

    RemovedGroupRomsBySource,

    ReGroupRomsBySource,

    UIMetadataGameplayTags,

    TransformInMeshModifier,

    SurfaceMetadataSlotNameIndexToName,

    BulkDataFilesNumFilesLimit,

    RemoveModifiersHack,

    SurfaceMetadataSerialized,

    FixesForMeshSectionMultipleOutputs,

    ImageParametersInServerBuilds,

    RemovedUnnecessarySerializationVersioning,

    AddTextureCompressionSettingCompilationInfo,

    RestructureConstantImageData,

    RestructureConstantMeshData,

    RestructureRomData,

    RestructureRomDataRemovingRomHash,

    ModifiedRomCompiledDataSerialization,

    ModelResourcesExtensionData,

    LODsPerComponent,

    LODsPerComponentTypeMismatch,

    ImageHiResLODsUseLODGroupInfo,

    MovedTableRowNoneGenerationToUnreal,

    RemoveObsoletMeshInterpolateAndGeometryOp,

    RemoveObsoleteDataTypesFromEnum,
    ConvertModelResourcesToUObject,

    RemoveObsoletImageGradientOp,

    MeshReferencesExtendedForCompilation,

    RemoveObsoleteBoolOps,

    AddOverlayMaterials,

    PrefetchHighQualityMipMaps,

    // -----<new versions can be added above this line>--------
    LastCustomizableObjectVersion
}
