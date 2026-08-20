using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CUE4Parse_Conversion.Dto;
using CUE4Parse_Conversion.Exporters;
using CUE4Parse_Conversion.Formats.Meshes;
using CUE4Parse_Conversion.Options;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Images;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Roms;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.Utils;

namespace CUE4Parse_Conversion.Mutable;

/// <summary>Holds a converted mutable mesh ready for disk write (replaces the deleted Mesh exporter wrapper).</summary>
public readonly record struct MutableMeshFile(string FileName, byte[] FileData);

public class MutableExporter : ExporterBase
{
    /// <summary>Skeleton name → list of (export path without extension, mesh file).</summary>
    public readonly Dictionary<string, List<(string Path, MutableMeshFile Mesh)>> Objects;
    public readonly List<CTexture> Images;
    public int meshIndex;

    // Temp flag to disable makeshift LOD grouping logic
    private bool exportAll = true;

    //TODO: make this a config that's passed in
    private bool exportImages = true;

    private readonly ExportOptions _options;
    private Dictionary<uint, string> surfaceNameMap = [];

    public MutableExporter(UCustomizableObject original, ExportOptions options, string? filterSkeletonName = null) : base(original)
    {
        Objects = [];
        Images = [];
        meshIndex = 0;
        _options = options;

        // <skeletonIndex, <MaterialSlot, Meshes>>
        Dictionary<uint, Dictionary<string, List<FMesh>>> meshes = [];

        var loader = new FMutableLoader(original);

        if (!original.Private.TryLoad(out UCustomizableObjectPrivate coPrivate) || !coPrivate.ModelResources.TryLoad(out UModelResources modelResources))
            return;

        surfaceNameMap = GetSurfaceNameMap(modelResources);

        for (uint index = 0; index < original.Model.Program.Roms.Length; index++)
        {
            var rom = original.Model.Program.Roms[index];
            switch (rom.Type)
            {
                case ERomDataType.Image:
                    if (exportImages)
                    {
                        var image = loader.LoadImage(index);
                        if (image != null) ExportMutableImage(image);
                    }
                    break;
                case ERomDataType.Mesh:
                    var mesh = loader.LoadMesh(index);
                    StoreMutableMesh(mesh, meshes, surfaceNameMap, index);
                    break;
                default:
                    Log.Information("Unknown resource type: {0} for index: {1}", rom.Type, index);
                    break;
            }
        }

        if (meshes.Count > 0)
            ExportMutableMeshes(original, meshes, modelResources.Skeletons, filterSkeletonName);
    }

    private Dictionary<uint, string> GetSurfaceNameMap(UModelResources modelResources)
    {
        Dictionary<uint, string> map = [];

        var meshMetadata = modelResources.MeshMetadata;
        var surfaceMetadata = modelResources.SurfaceMetadata;
        if (meshMetadata == null || surfaceMetadata == null) return map;

        foreach (var meshEntry in meshMetadata.Properties)
        {
            var surfaceID = meshEntry.Value.GetValue<FStructFallback>().Get<uint>("SurfaceMetadataId");
            var surfaceEntry = surfaceMetadata.Properties.First(key => key.Key.GetValue<uint>() == Convert.ToUInt32(surfaceID));
            var materialSlotName = surfaceEntry.Value.GetValue<FStructFallback>().Get<FName>("MaterialSlotName").PlainText;
            map.Add(meshEntry.Key.GetValue<uint>(), materialSlotName);
        }

        return map;
    }

    private void StoreMutableMesh(FMesh mesh, Dictionary<uint, Dictionary<string, List<FMesh>>> meshes, Dictionary<uint, string> surfaceNameMap, uint romIndex)
    {
        // var skeletonIndex = mesh.SkeletonIDs.LastOrDefault(0u);
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
            var skeletonSoftObject = skeletons[0];
            var skeletonName = skeletonSoftObject.AssetPathName.PlainText.SubstringAfterLast(".");
            if (filterSkeletonName != null &&
                !skeletonName.Contains(filterSkeletonName, StringComparison.OrdinalIgnoreCase)) continue;

            if (exportAll || skeletonName.Contains("Wheel", StringComparison.OrdinalIgnoreCase) || skeletonName.Contains("Shoe", StringComparison.OrdinalIgnoreCase) || ObjectName.StartsWith("CO_Figure"))
            {
                foreach (var materialGroup in skeletonGroup.Value)
                {
                    if (materialGroup.Key.Contains("LOD", StringComparison.OrdinalIgnoreCase)) continue;

                    if (exportAll || materialGroup.Key.Equals("Wheel", StringComparison.OrdinalIgnoreCase) || materialGroup.Key.Equals("UNNAMED", StringComparison.OrdinalIgnoreCase) || skeletonName.Equals("SK_Figure"))
                        materialGroup.Value.ForEach(mesh =>
                            ExportMutableMesh(originalCustomizableObject, [mesh], materialGroup.Key, skeletonSoftObject, true, skeletonGroup.Key));
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

    private void ExportMutableMesh(UCustomizableObject originalCustomizableObject, List<FMesh> meshes, string materialSlotName, FSoftObjectPath skeletonSoftObject, bool appendId = false, uint romIndex = 0)
    {
        var mesh = meshes[0];
        meshes.RemoveAt(0);

        var subMeshId = mesh.Surfaces[0].SubMeshes[0].ExternalId;
        var matName = exportAll ? surfaceNameMap.GetValueOrDefault(subMeshId, subMeshId.ToString()) : materialSlotName;

        // if (!mesh.TryConvert(originalCustomizableObject, matName, out CSkeletalMesh convertedMesh, meshes) || convertedMesh.LODs.Count == 0)
        if (!mesh.TryConvert(originalCustomizableObject, matName, out StaticMeshDto convertedMesh, meshes) || convertedMesh.LODs.Count == 0)
        {
            Log.Warning("Mesh '{ObjectName}.{Skeleton}.{Mat}' has no LODs", ObjectName, skeletonSoftObject.AssetPathName.PlainText, matName);
            return;
        }

        var skeletonName = skeletonSoftObject.AssetPathName.PlainText.SubstringAfterLast(".");
        if (skeletonSoftObject.TryLoad(out USkeleton loadedSkeleton))
        {
            skeletonName = loadedSkeleton.Name;
        }

        var meshName = $"{skeletonName.Replace("_Skeleton", "")}_{matName}";
        if (appendId)
        {
            var lod0 = convertedMesh.LODs[0];
            meshName = $"{meshIndex++:D4}_{romIndex:D5}_{matName}_{lod0.Vertices.Length}_{lod0.Indices.Length}";
        }
        var exportPath = $"{skeletonName}/{meshName}";

        if (_options.MeshFormat == EMeshFormat.UEFormat)
        {
            var files = new UEFormatMeshFormat().BuildStaticMesh(meshName, ObjectPath, _options, convertedMesh);
            if (files.Count == 0)
            {
                convertedMesh.Dispose();
                return;
            }

            var outputMesh = new MutableMeshFile($"{meshName}.uemodel", files[0].Data);

            if (!Objects.ContainsKey(skeletonName))
                Objects.Add(skeletonName, []);

            Objects[skeletonName].Add((exportPath, outputMesh));
            convertedMesh.Dispose();
            return;
        }

        convertedMesh.Dispose();
        // TODO: other mesh formats
    }

    private void ExportMutableImage(FImage image)
    {
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

    protected override IReadOnlyList<ExportFile> BuildExportFiles(CancellationToken ct = default)
    {
        var results = new List<ExportFile>();
        foreach (var (_, meshes) in Objects)
        {
            foreach (var (path, mesh) in meshes)
            {
                ct.ThrowIfCancellationRequested();
                var suffix = "/" + path.Replace('\\', '/');
                results.Add(new ExportFile("uemodel", mesh.FileData, suffix));
            }
        }

        for (var i = 0; i < Images.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var data = Images[i].Encode(_options, out var ext);
            results.Add(new ExportFile(ext, data, $"/textures/{i:D4}_{Images[i].PixelFormat}"));
        }

        if (results.Count == 0)
            throw new Exception("Mutable export produced no files");

        return results;
    }
}
