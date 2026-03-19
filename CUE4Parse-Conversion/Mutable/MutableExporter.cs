using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse_Conversion.Meshes.UEFormat;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Images;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Roms;
using CUE4Parse.UE4.Assets.Exports.Texture;
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
    public readonly Dictionary<uint, CTexture> ImagesWithIDs;
    public int meshIndex;
    public int imageIndex;

    public List<MutableMeshDef> DebugMeshes;
    public List<MutableImageDef> DebugImages;
    /// <summary> Key = MeshIDPrefix from Program.ConstantMeshes (UE groups LODs by this). </summary>
    public Dictionary<uint, List<MutableMeshDef>> DebugMeshGrouping;
    public Dictionary<int, List<MutableImageDef>> DebugImageGrouping;

    /// <summary> ROM index -> constant mesh index (grouping key from Program.ConstantMeshes). </summary>
    private Dictionary<uint, int> _romIndexToConstantMeshIndex;

    // Flag to disable makeshift LOD grouping logic
    private bool exportAll = true;

    private Dictionary<uint, string> surfaceNameMap;

    public MutableExporter(UCustomizableObject original, ExporterOptions options, string? filterSkeletonName = null) : base(original, options)
    {
        Objects = [];
        Images = [];
        ImagesWithIDs = [];
        meshIndex = 0;
        imageIndex = 0;

        DebugMeshes = [];
        DebugImages = [];
        DebugMeshGrouping = [];
        DebugImageGrouping = [];
        _romIndexToConstantMeshIndex = [];

        // <skeletonIndex, <MaterialSlot, Meshes>>
        Dictionary<uint, Dictionary<string, List<FMesh>>> meshes = [];
        // GroupID, Images
        Dictionary<uint, List<FImage>> imagesByGroup = [];

        var loader = new FMutableLoader(original);
        var romId = loader.GetRomIdentification();

        if (!original.Private.TryLoad(out UCustomizableObjectPrivate coPrivate) || !coPrivate.ModelResources.TryLoad(out UModelResources modelResources))
            return;

        surfaceNameMap = GetSurfaceNameMap(modelResources);

        var exportImages = true; //TODO: make this a config that's passed in
        
        for (uint index = 0; index < original.Model.Program.Roms.Length; index++)
        {
            var rom = original.Model.Program.Roms[index];
            switch (rom.Type)
            {
                case ERomDataType.Image:
                    if (exportImages)
                    {
                        // HighRes flag or CO.Model.Program.ConstantImages(FirstIndex)
                        var image = loader.LoadImage(index);
                        var img = romId.GetImageRomIdentity(index);
                        // if (img == null) continue;

                        // var groupId = img.Value.ImageGroupId;  // same for all LODs of this image
                        // if (!imagesByGroup.TryGetValue(groupId, out var list))
                        //     imagesByGroup[groupId] = list = [];

                        // list.Add(image);
                        var groupId = -1;
                        if (img != null)
                        {
                            groupId = (int) img.Value.ImageGroupId;
                        }
                        else
                        {
                            Log.Error("Failed to find RomIdentity for Image ROM at index {0}", index);
                        }
                        ExportMutableImage(image, index, groupId);
                    }
                    break;
                case ERomDataType.Mesh:
                    var mesh = loader.LoadMesh(index);
                    var meshRomIdentity = romId.GetMeshRomIdentity(index);
                    int? constantMeshIndex = romId.GetMeshConstantIndex(index);
                    if (constantMeshIndex.HasValue)
                        _romIndexToConstantMeshIndex[index] = constantMeshIndex.Value;
                    if (meshRomIdentity != null)
                    {
                        var id = meshRomIdentity.Value;
                        mesh.MeshIDPrefix = id.MeshIDPrefix;
                        mesh.SkeletonIDs = id.SkeletonConstantIndex >= 0 ? [(uint)id.SkeletonConstantIndex] : mesh.SkeletonIDs ?? [];
                    }
                    else if (constantMeshIndex.HasValue && original.Model?.Program?.ConstantMeshes != null && constantMeshIndex.Value >= 0 && constantMeshIndex.Value < original.Model.Program.ConstantMeshes.Length)
                    {
                        var range = original.Model.Program.ConstantMeshes[constantMeshIndex.Value];
                        mesh.MeshIDPrefix = range.MeshIDPrefix;
                    }
                    else
                    {
                        Log.Error("Failed to find RomIdentity for Mesh ROM at index {0}", index);
                        // mesh.MeshIDPrefix and mesh.SkeletonIDs remain as read from the streamed FMesh
                    }
                    StoreMutableMeshForTesting(mesh, meshes, surfaceNameMap, index);
                    break;
                default:
                    Log.Information("Unknown resource type: {0} for index: {1}", rom.Type, index);
                    break;
            }
        }

        if (meshes.Count > 0)
            ExportMutableMeshes(original, meshes, modelResources.Skeletons, filterSkeletonName);

        // if (imagesByGroup.Count > 0)
        //     ExportMutableImages(imagesByGroup);
    }

    public MutableExporter(UCustomizableObjectInstance instance, UCustomizableObject parent, ExporterOptions options) : base(instance, options)
    {
        Objects = [];
        Images = [];
        ImagesWithIDs = [];
        meshIndex = 0;
        imageIndex = 0;

        DebugMeshes = [];
        DebugImages = [];
        DebugMeshGrouping = [];
        DebugImageGrouping = [];
        
        // <skeletonIndex, <MaterialSlot, Meshes>>
        Dictionary<uint, Dictionary<string, List<FMesh>>> meshes = [];
        
        if (!parent.Private.TryLoad(out UCustomizableObjectPrivate coPrivate) || !coPrivate.ModelResources.TryLoad(out UModelResources modelResources))
            return;

        surfaceNameMap = GetSurfaceNameMap(modelResources);
        var loader = new FMutableLoader(parent);
        
        var resolver = new ParameterToResourceResolver(parent.Model.Program);
        var intParams = instance.Descriptor.IntParameters
            .Select(p => (p.ParameterName, p.ParameterValueName))
            .ToList();
        if (intParams.Count > 0)
            MutableResolverDebugLog.Log($"INSTANCE_INT_PARAMS\tcount={intParams.Count}\n" + string.Join("\n", intParams.Select(p => $"\t{p.ParameterName}={p.ParameterValueName}")));

        var (imageConstants, meshConstants, imageRoms, meshRoms) = resolver.ResolveFromInstanceIntParameters(intParams);

        // foreach (var imgConstant in imageConstants)
        // {
        //     var img = parent.Model.Program.ConstantImages[imgConstant];
        //     var imgRom = parent.Model.Program.Roms[img.FirstIndex];
        // }
        
        foreach (var meshRomIndex in meshRoms)
        {
            var meshRom = parent.Model.Program.Roms[meshRomIndex];

            if (meshRom.Type != ERomDataType.Mesh)
            {
                Log.Error("ROM at index {0} does not contain a mesh");
                continue;
            }
            var mesh = loader.LoadMesh(meshRomIndex);
            StoreMutableMeshForTesting(mesh, meshes, surfaceNameMap, meshRomIndex);
        }
        
        foreach (var imageRomIndex in imageRoms)
        {
            var imageRom = parent.Model.Program.Roms[imageRomIndex];

            if (imageRom.Type != ERomDataType.Image)
            {
                Log.Error("ROM at index {0} does not contain a image");
                continue;
            }
            var image = loader.LoadImage(imageRomIndex);
            ExportMutableImage(image, imageRomIndex, 0);
        }
        
        if (meshes.Count > 0)
            ExportMutableMeshes(parent, meshes, modelResources.Skeletons, null);
    }

    private Dictionary<uint, string> GetSurfaceNameMap(UModelResources modelResources)
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

        // if (mesh.Surfaces == null || mesh.Surfaces.Length == 0 || mesh.Surfaces[0].SubMeshes.Length == 0 ||
        //     !surfaceNameMap.TryGetValue(mesh.Surfaces[0].SubMeshes[0].ExternalId, out var materialSlotName)) return;
        if (mesh.Surfaces == null || mesh.Surfaces.Length == 0 || mesh.Surfaces[0].SubMeshes.Length == 0) return;
        var materialSlotName = mesh.MeshIDPrefix.ToString();

        // // TODO: Remove temp limit
        // if (materialSlotName.Contains("LOD", StringComparison.OrdinalIgnoreCase)) return;

        if (!meshes.ContainsKey(skeletonIndex))
            meshes[skeletonIndex] = [];

        // if (exportAll) materialSlotName = "Mesh";
        
        if (!meshes[skeletonIndex].ContainsKey(materialSlotName))
            meshes[skeletonIndex][materialSlotName] = [];

        meshes[skeletonIndex][materialSlotName].Add(mesh);
    }

    private void StoreMutableMeshForTesting(FMesh mesh, Dictionary<uint, Dictionary<string, List<FMesh>>> meshes, Dictionary<uint, string> surfaceNameMap, uint romIndex)
    {
        var skeletonIndex = romIndex;

        if (mesh.Surfaces == null || mesh.Surfaces.Length == 0 || mesh.Surfaces[0].SubMeshes.Length == 0 ||
            !surfaceNameMap.TryGetValue(mesh.Surfaces[0].SubMeshes[0].ExternalId, out var materialSlotName)) return;

        // TODO: Remove temp limit
        if (materialSlotName.Contains("LOD", StringComparison.OrdinalIgnoreCase)) return;

        if (!meshes.ContainsKey(skeletonIndex))
            meshes[skeletonIndex] = [];

        if (exportAll) materialSlotName = "Mesh";
        
        if (!meshes[skeletonIndex].ContainsKey(materialSlotName))
            meshes[skeletonIndex][materialSlotName] = [];

        meshes[skeletonIndex][materialSlotName].Add(mesh);
    }

    private void ExportMutableMeshes(UCustomizableObject originalCustomizableObject, Dictionary<uint, Dictionary<string, List<FMesh>>> meshes,
        FSoftObjectPath[] skeletons, string? filterSkeletonName)
    {
        foreach (var skeletonGroup in meshes)
        {
            var skeletonIndex = 0;
            // if (skeletonIndex > skeletons.Length) skeletonIndex = skeletons.Length - 1;
            var skeletonSoftObject = skeletons[skeletonIndex];
            var skeletonName = skeletonSoftObject.AssetPathName.PlainText.SubstringAfterLast(".");
            if (filterSkeletonName != null &&
                !skeletonName.Contains(filterSkeletonName, StringComparison.OrdinalIgnoreCase)) continue;

            foreach (var materialGroup in skeletonGroup.Value)
            {
                materialGroup.Value.ForEach(mesh =>
                    ExportMutableMesh(originalCustomizableObject, [mesh], materialGroup.Key, skeletonSoftObject, true, skeletonGroup.Key));
            }

            // if (exportAll || skeletonName.Contains("Wheel", StringComparison.OrdinalIgnoreCase) || skeletonName.Contains("Shoe", StringComparison.OrdinalIgnoreCase) || ExportName.StartsWith("CO_Figure"))
            // {
            //     foreach (var materialGroup in skeletonGroup.Value)
            //     {
            //         if (materialGroup.Key.Contains("LOD", StringComparison.OrdinalIgnoreCase)) continue;

            //         if (exportAll || materialGroup.Key.Equals("Wheel", StringComparison.OrdinalIgnoreCase) || materialGroup.Key.Equals("UNNAMED", StringComparison.OrdinalIgnoreCase) || skeletonName.Equals("SK_Figure"))
            //             materialGroup.Value.ForEach(mesh =>
            //                 ExportMutableMesh(originalCustomizableObject, [mesh], materialGroup.Key, skeletonSoftObject, true));
            //         else
            //         {
            //             var sortedList = materialGroup.Value.OrderByDescending(mesh => mesh.VertexBuffers.ElementCount)
            //                 .ToList();
            //             ExportMutableMesh(originalCustomizableObject, sortedList, materialGroup.Key, skeletonSoftObject);
            //         }
            //     }
            // }
            // else
            // {
            //     foreach (var materialGroup in skeletonGroup.Value)
            //     {
            //         var sortedList = materialGroup.Value.OrderByDescending(mesh => mesh.VertexBuffers.ElementCount)
            //             .ToList();
            //         ExportMutableMesh(originalCustomizableObject, sortedList, materialGroup.Key, skeletonSoftObject);
            //     }
            // }
        }
    }

    private void ExportMutableMesh(UCustomizableObject originalCustomizableObject, List<FMesh> meshes, string materialSlotName, FSoftObjectPath skeletonSoftObject, bool appendId = false, uint romIndex = 0)
    {
        var mesh = meshes[0];
        meshes.RemoveAt(0);

        var subMeshId = mesh.Surfaces[0].SubMeshes[0].ExternalId;
        var matName = exportAll ? surfaceNameMap.GetValueOrDefault(subMeshId, subMeshId.ToString()) : materialSlotName;
        
        // if (!mesh.TryConvert(originalCustomizableObject, matName, out CSkeletalMesh convertedMesh, meshes) || convertedMesh.LODs.Count == 0)
        if (!mesh.TryConvert(originalCustomizableObject, matName, out CStaticMesh convertedMesh, meshes) || convertedMesh.LODs.Count == 0)
        {
            Log.Warning($"Mesh '{ExportName}.{skeletonSoftObject.AssetPathName.PlainText}.{matName} (ROM Index {romIndex})' has no LODs");
            return;
        }

        USkeleton skeleton = null;
        var skeletonName = skeletonSoftObject.AssetPathName.PlainText.SubstringAfterLast(".");
        if (skeletonSoftObject.TryLoad(out skeleton))
        {
            skeletonName = skeleton.Name;
        }

        var meshName = $"{skeletonName.Replace("_Skeleton", "")}_{matName}";
        // var meshName = materialSlotName;
        if (appendId) meshName = $"{meshIndex++:D4}_{romIndex:D5}_{matName}_{convertedMesh.LODs[0].NumVerts}_{convertedMesh.LODs[0].Indices.Value.Length}";
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
            // new UEModel(meshName, convertedMesh, null, totalSockets.ToArray(), skeletonSoftObject, null, Options).Save(ueModelArchive);
            new UEModel(meshName, convertedMesh, new FPackageIndex(), Options).Save(ueModelArchive);
            var outputMesh = new Mesh($"{meshName}.uemodel", ueModelArchive.GetBuffer(), convertedMesh.LODs[0].GetMaterials(Options));

            if (!Objects.ContainsKey(skeletonName))
                Objects.Add(skeletonName, []);

            Objects[skeletonName].Add(new Tuple<string, Mesh>(exportPath, outputMesh));

            // DO NOT MOVE
            var meshDef = new MutableMeshDef()
            {
                RomType = ERomDataType.Mesh,
                AssetIndex = meshIndex - 1,
                RomIndex = romIndex,
                MeshName = meshName,
                Mesh = mesh,
                ConvertedMesh = outputMesh
            };
                
            if (!DebugMeshGrouping.TryGetValue(mesh.MeshIDPrefix, out var list))
                DebugMeshGrouping[mesh.MeshIDPrefix] = list = [];

            list.Add(meshDef);
            
            DebugMeshes.Add(meshDef);
            return;
        }
        // TODO: other types
    }

    // private void ExportMutableImages(Dictionary<uint, List<FImage>> imagesByGroup)
    // {
    //     foreach (var imageGroup in imagesByGroup)
    //     {
    //         var image = imageGroup.Value.OrderByDescending(image => image.DataStorage.Size).FirstOrDefault();
    //         if (image == null) continue;
    //         ExportMutableImage(image);
    //     }
    // }

    private void ExportMutableImage(FImage image, uint romIndex, int groupId)
    {
        var resolution = image.DataStorage.Size;

        try
        {
            var bitmap = image.Decode();
            if (bitmap != null) 
            {
                Images.Add(bitmap);
                ImagesWithIDs.Add(romIndex, bitmap);

                // DO NOT MOVE
                var imageDef = new MutableImageDef()
                {
                    RomType = ERomDataType.Image,
                    AssetIndex = imageIndex++,
                    RomIndex = romIndex,
                    GroupId = groupId,
                    PixelFormat = bitmap.PixelFormat,
                    Image = image,
                    DecodedImage = bitmap
                };
                
                if (!DebugImageGrouping.TryGetValue(groupId, out var list))
                    DebugImageGrouping[groupId] = list = [];

                list.Add(imageDef);
                
                DebugImages.Add(imageDef);
            }
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

public class MutableAssetDef
{
    public int AssetIndex;
    public uint RomIndex;
    public ERomDataType RomType;

    public override string ToString()
    {
        return $"Rom:{RomIndex}, Asset:{AssetIndex}, Data:{base.ToString()}";
    }
}

public class MutableMeshDef : MutableAssetDef
{
    public string MeshName;
    public FMesh Mesh;
    public Mesh ConvertedMesh;
}

public class MutableImageDef : MutableAssetDef
{
    public EPixelFormat PixelFormat;
    public int GroupId;
    public FImage Image;
    public CTexture DecodedImage;
}
