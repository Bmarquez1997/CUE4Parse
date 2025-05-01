using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Meshes.UEFormat;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Image;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Writers;
using CUE4Parse.Utils;
using Serilog;
using SkiaSharp;

namespace CUE4Parse_Conversion.Mutable;

public class MutableExporter : ExporterBase
{
    // <SkeletonName, (MeshName, Mesh)>
    public readonly Dictionary<string, List<Tuple<string, Mesh>>> Objects;
    public readonly List<CTexture> Images;
    
    public MutableExporter(UCustomizableObject originalCustomizableObject, ExporterOptions options, AbstractVfsFileProvider provider, string? filterSkeletonName = null) : base(originalCustomizableObject, options)
    {
        Objects = [];
        Images = [];

        // <skeletonIndex, <MaterialSlot, Meshes>>
        Dictionary<uint, Dictionary<string, List<FMesh>>> meshes = [];
        
        var loader = new FMutableLoader(originalCustomizableObject);
        //var opCodes = loader.ReadByteCode();
        
        var coPrivate = originalCustomizableObject.Private.Load<UCustomizableObjectPrivate>();
        var modelResources = coPrivate.ModelResources;
        var surfaceNameMap = GetSurfaceNameMap(modelResources);

        for (uint index = 0; index < originalCustomizableObject.Model.Program.Roms.Length; index++)
        {
            var rom = originalCustomizableObject.Model.Program.Roms[index];
            switch (rom.ResourceType)
            {
                case ERomDataType.Image:
                    // var image = loader.LoadImage(index);
                    // ExportMutableImage(image);
                    break;
                case ERomDataType.Mesh:
                    var mesh = loader.LoadMesh(index);
                    StoreMutableMesh(mesh, meshes, surfaceNameMap);
                    break;
                default:
                    Log.Information("Unknown resource type: {0} for index: {1}", rom.ResourceType, index);
                    break;
            }
        }

        if (meshes.Count > 0)
            ExportMutableMeshes(originalCustomizableObject, meshes, modelResources.Skeletons, filterSkeletonName);
    }
    
    private Dictionary<uint, string> GetSurfaceNameMap(FModelResources modelResources)
    {
        Dictionary<uint, string> surfaceNameMap = [];
        
        var meshMetadata = modelResources.MeshMetadata;
        var surfaceMetadata = modelResources.SurfaceMetadata;
        if (meshMetadata == null || surfaceMetadata == null) return surfaceNameMap;

        foreach (var meshEntry in meshMetadata.Properties)
        {
            var surfaceID = meshEntry.Value.GetValue<FStructFallback>().Get<uint>("SurfaceMetadataId");
            var surfaceEntry = surfaceMetadata.Properties.First(key => key.Key.GetValue<uint>() == Convert.ToUInt32(surfaceID));
            var materialSlotName = surfaceEntry.Value.GetValue<FStructFallback>().Get<FName>("MaterialSlotName").PlainText;
            surfaceNameMap.Add(meshEntry.Key.GetValue<uint>(), materialSlotName);
        }

        return surfaceNameMap;
    }
    
    private void StoreMutableMesh(FMesh mesh, Dictionary<uint, Dictionary<string, List<FMesh>>> meshes, Dictionary<uint, string> surfaceNameMap)
    {
        var skeletonIndex = mesh.SkeletonIDs.LastOrDefault(0u);

        if (mesh.Surfaces.Length == 0 || mesh.Surfaces[0].SubMeshes.Length == 0 || 
            !surfaceNameMap.TryGetValue(mesh.Surfaces[0].SubMeshes[0].ExternalId, out var materialSlotName)) return;
        
        // TODO: Remove temp limit
        if (materialSlotName.Contains("LOD", StringComparison.OrdinalIgnoreCase)) return;

        if (!meshes.ContainsKey(skeletonIndex))
            meshes[skeletonIndex] = [];

        if (!meshes[skeletonIndex].ContainsKey(materialSlotName))
            meshes[skeletonIndex][materialSlotName] = [];

        meshes[skeletonIndex][materialSlotName].Add(mesh);
    }

    private void ExportMutableMeshes(UCustomizableObject originalCustomizableObject, Dictionary<uint, Dictionary<string, List<FMesh>>> meshes,
        FSoftObjectPath[] skeletons, string? filterSkeletonName)
    {
        foreach (var skeletonGroup in meshes)
        {
            var skeletonSoftObject = skeletons[skeletonGroup.Key];
            var skeletonName = skeletonSoftObject.AssetPathName.PlainText.SubstringAfterLast(".");
            if (filterSkeletonName != null && 
                !skeletonName.Contains(filterSkeletonName, StringComparison.OrdinalIgnoreCase)) continue;
            
            if (skeletonName.Contains("Wheel", StringComparison.OrdinalIgnoreCase) || skeletonName.Contains("Shoe", StringComparison.OrdinalIgnoreCase) || ExportName.StartsWith("CO_Figure"))
            {
                foreach (var materialGroup in skeletonGroup.Value)
                {
                    if (materialGroup.Key.Contains("LOD", StringComparison.OrdinalIgnoreCase)) continue;
                    
                    if (materialGroup.Key.Equals("Wheel", StringComparison.OrdinalIgnoreCase) || materialGroup.Key.Equals("UNNAMED", StringComparison.OrdinalIgnoreCase) || skeletonName.Equals("SK_Figure"))
                        materialGroup.Value.ForEach(mesh =>
                            ExportMutableMesh(originalCustomizableObject, [mesh], materialGroup.Key, skeletonSoftObject, true));
                    else
                    {
                        var sortedList = materialGroup.Value.OrderByDescending(mesh => mesh.VertexBuffers.ElementCount)
                            .ToList();
                        ExportMutableMesh(originalCustomizableObject, sortedList, materialGroup.Key, skeletonSoftObject);
                    }
                }
            }
            else
            {
                foreach (var materialGroup in skeletonGroup.Value)
                {
                    var sortedList = materialGroup.Value.OrderByDescending(mesh => mesh.VertexBuffers.ElementCount)
                        .ToList();
                    ExportMutableMesh(originalCustomizableObject, sortedList, materialGroup.Key, skeletonSoftObject);
                }
            }
        }
    }
    
    private void ExportMutableMesh(UCustomizableObject originalCustomizableObject, List<FMesh> meshes, string materialSlotName, FSoftObjectPath skeletonSoftObject, bool appendId = false)
    {
        var mesh = meshes[0];
        meshes.RemoveAt(0);

        if (meshes.Count == 0 && mesh.VertexBuffers.ElementCount <= 800) return;
        
        if (!mesh.TryConvert(originalCustomizableObject, materialSlotName, out var convertedMesh, meshes) || convertedMesh.LODs.Count == 0)
        {
            Log.Logger.Warning($"Mesh '{ExportName}' has no LODs");
            return;
        }

        USkeleton skeleton = null;
        var skeletonName = skeletonSoftObject.AssetPathName.PlainText.SubstringAfterLast(".");
        if (skeletonSoftObject.TryLoad(out skeleton))
        {
            skeletonName = skeleton.Name;
        }
        
        var meshName = $"{skeletonName.Replace("_Skeleton", "")}_{materialSlotName}";
        // var meshName = materialSlotName;
        if (appendId) meshName = $"{materialSlotName}_{convertedMesh.LODs[0].NumVerts}_{mesh.MeshIDPrefix}_{mesh.ReferenceID}";
        var exportPath = $"{skeletonName}/{meshName}";
        
        var totalSockets = new List<FPackageIndex>();
        if (Options.SocketFormat != ESocketFormat.None && skeleton != null)
        {
            totalSockets.AddRange(skeleton.Sockets);
        }

        if (Options.MeshFormat == EMeshFormat.UEFormat)
        {
            using var ueModelArchive = new FArchiveWriter();
            // var skeletonPackageIndex = new FPackageIndex(skeletonSoftObject.Owner, 0);
            new UEModel(meshName, convertedMesh, null, totalSockets.ToArray(), skeletonSoftObject, null, Options).Save(ueModelArchive);
            var outputMesh = new Mesh($"{meshName}.uemodel", ueModelArchive.GetBuffer(), convertedMesh.LODs[0].GetMaterials(Options));
            
            if (!Objects.ContainsKey(skeletonName))
                Objects.Add(skeletonName, []);
                
            Objects[skeletonName].Add(new Tuple<string, Mesh>(exportPath, outputMesh));
            return;
        }
        // TODO: other types
    }

    private void ExportMutableImage(FImage image)
    {
        if (image == null) return;
        var resolution = image.DataStorage.ImageSize;

        switch (image.DataStorage.ImageFormat)
        {
            // Temporary LOD exclusion
            case EImageFormat.BC5 when resolution is { X: < 760, Y: < 760 }:
            case EImageFormat.BC3 when resolution is { X: < 760, Y: < 760 } && resolution.Y != 576:
                return;
        }
        try
        {
            var bitmap = image.Decode();
            if (bitmap != null) Images.Add(bitmap);
        }
        catch (Exception e)
        {
            Log.Error("Exception thrown decoding mutable image: {0}", e.Message);
        }
    }

    public override bool TryWriteToDir(DirectoryInfo baseDirectory, out string label, out string savedFilePath)
    {
        savedFilePath = "TempFilePath";
        label = "Mutable";
        return false;
    }

    public override bool TryWriteToZip(out byte[] zipFile)
    {
        throw new System.NotImplementedException();
    }

    public override void AppendToZip()
    {
        throw new System.NotImplementedException();
    }
}
