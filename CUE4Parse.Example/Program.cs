using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Animations;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Mutable;
using CUE4Parse_Conversion.Textures;
using CUE4Parse_Conversion.UEFormat.Enums;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;
using CUE4Parse.UE4.Assets.Exports.Nanite;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;
using Newtonsoft.Json;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace CUE4Parse.Example
{
    public static class Program
    {
        private const string _gameDirectory = "A:\\Old_FN_Builds\\39.30\\Paks"; // Change game directory path to the one you have.
        private const string _aesKey = "0x413222CE49E03E5B13F6A0C812BA4C095E3816809AA51F96AD59E40C3F8D5CA0";

        private const string _mapping = "C:\\Users\\Bmarq\\AppData\\Local\\FortnitePorting\\.data\\++Fortnite+Release-39.30-CL-50141518.usmap";
        private const string _objectPath = "FortniteGame/Plugins/GameFeatures/Juno/FigureCosmetics/Content/Figure/_R39/CO/CO_Figure_R39";
        
        private const string ChubbyJingleInstancePath = "FortniteGame/Plugins/GameFeatures/Juno/FigureCosmetics/Content/Figure/Figure_ChubbyJingle/Mutable/COI_Figure_ChubbyJingle";
        private const string SoloSnoozeInstancePath = "FortniteGame/Plugins/GameFeatures/Juno/FigureCosmetics/Content/Figure/Figure_SoloSnooze/Mutable/COI_Figure_SoloSnooze";

        private static int[] SkeletonBodyIDs = [0, 102, 219, 312, 405, 498];
        private static int[] HeadIDs = [205, 301, 394, 487, 580, 592];
        
        private static int[] BodyRomIDs = [5573, 5928, 6190, 6386, 6583, 6784];
        private static int[] HeadRomIDs = [6121, 6346, 6542, 6742, 6944, 6969];
        private static int[] ChubbyJingleHeadRomIDs = [6165, 6364, 6562, 6762, 6964, 6993];
        private static int[] SoloSnoozeHeadAccRomIDs = [5688, 5994, 6248, 6442, 6640, 6840];
        
        private static int[] ChubbyJingleTextureRomIDs = [895, 1340, 1775, 2159, 2651, 3144, 3623];
        private static int[] SoloSnoozeTextureRomIDs = [659, 1050, 1495, 1925, 2334, 2826, 3318, 3753];

        private static string _objectInstancePath = ChubbyJingleInstancePath;
        private static int[] _meshRomIDs = ChubbyJingleHeadRomIDs;
        private static int[] _textureRomIDs = ChubbyJingleTextureRomIDs;

        // private static string _objectInstancePath = SoloSnoozeInstancePath;
        // private static int[] _meshRomIDs = SoloSnoozeHeadAccRomIDs;
        // private static int[] _textureRomIDs = SoloSnoozeTextureRomIDs;

        public static void Main()
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console(theme: AnsiConsoleTheme.Literate).CreateLogger();

            var provider = new DefaultFileProvider(_gameDirectory, SearchOption.TopDirectoryOnly, true, new VersionContainer(EGame.GAME_UE5_8));
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(_mapping, StringComparer.Ordinal);

            provider.Initialize(); // will scan local files and read them to know what it has to deal with (PAK/UTOC/UCAS/UASSET/UMAP)
            provider.SubmitKey(new FGuid(), new FAesKey(_aesKey)); // decrypt basic info (1 guid - 1 key)
            InitOodle();

            // Enable resolver debug log to file in workspace (current directory when run from repo) so it can be read in follow-up iterations
            var debugLogPath = Path.Combine(Environment.CurrentDirectory, "mutable_resolver_debug.log");
            MutableResolverDebugLog.LogPath = debugLogPath;
            MutableResolverDebugLog.Reset();
            Log.Information("Mutable resolver debug log: {Path}", Path.GetFullPath(debugLogPath));

            var coAsset = provider.LoadPackageObject<UCustomizableObject>(_objectPath);
            if (coAsset == null) throw new Exception("Failed to read CO asset");
            
            var coiAsset = provider.LoadPackageObject<UCustomizableObjectInstance>(_objectInstancePath);
            if (coiAsset == null) throw new Exception("Failed to read COI asset");

            
            // Logic pulled from MutableExporter, determine ROM/Constant IDs and validate
            var resolver = new ParameterToResourceResolver(coAsset.Model.Program);
            var intParams = coiAsset.Descriptor.IntParameters
                .Select(p => (p.ParameterName, p.ParameterValueName))
                .ToList();
            if (intParams.Count > 0)
                MutableResolverDebugLog.Log($"INSTANCE_INT_PARAMS\tcount={intParams.Count}\n" + string.Join("\n", intParams.Select(p => $"\t{p.ParameterName}={p.ParameterValueName}")));
            
            var (imageConstants, meshConstants, imageRoms, meshRoms) = resolver.ResolveFromInstanceIntParameters(intParams);
            
            if (meshConstants.Count == 0 && (meshRoms?.Count ?? 0) == 0)
                Log.Error("No mesh constants or ROMs found");
            
            if (imageConstants.Count == 0 && (imageRoms?.Count ?? 0) == 0)
                Log.Error("No image constants or ROMs found");
            
            if (_meshRomIDs.Any(mesh => !meshRoms.Contains(uint.Parse(mesh.ToString()))))
                Log.Error("Required meshes missing. Returned indices: " + string.Join("\n", meshRoms.Select(p => $"{p}, ")));
            
            if (_textureRomIDs.Any(img => !imageRoms.Contains(uint.Parse(img.ToString()))))
                Log.Error("Required textures missing. Returned indices: " + string.Join("\n", imageRoms.Select(p => $"{p}, ")));

            
            // // Read the COI and only export the related meshes and textures
            // var coiExporter = new MutableExporter(coiAsset, coAsset, GetExporterOptions());
            //
            // foreach (var mutableObject in coiExporter.Objects)
            //     ExportMeshes(mutableObject.Value);
            //
            // foreach (var (id, image) in coiExporter.ImagesWithIDs)
            //     ExportMutableImage(image, (int) id);

            
            // // Export whole CO asset and validate results
            // var mutableExporter = new MutableExporter(coAsset, GetExporterOptions());
            // var debugMeshes = mutableExporter.DebugMeshes;
            // var debugImages = mutableExporter.DebugImages;
            //
            // Mesh tests
            // if (debugMeshes.Count < 606) //702 if every mesh has 6 LODs, 117 objects total
            //     Log.Error("Mesh count incorrect.  Expected: >= {0}, Actual: {1}", 606, debugMeshes.Count);
            //
            // if (mutableExporter.DebugMeshGrouping.Count != 117)
            //     Log.Error("Mesh group count incorrect.  Expected: {0}, Actual: {1}", 117, mutableExporter.DebugMeshGrouping.Count);
            //
            // if (debugMeshes.Any(mesh => mesh.Mesh.MeshIDPrefix == uint.Parse("0")))
            //     Log.Error("Mesh is missing ID Prefix");
            //
            // if (HasInvalidMeshIdGrouping(debugMeshes, SkeletonBodyIDs))
            //     Log.Error("MeshPrefixID invalid for Skeleton Body mesh");
            //
            // if (HasInvalidMeshIdGrouping(debugMeshes, HeadIDs))
            //     Log.Error("MeshPrefixID invalid for Head mesh");
            //
            // // Image tests
            // if (debugImages.Count < 5550)
            //     Log.Error("Image count incorrect.  Expected: >= {0}, Actual: {1}", 5550, debugImages.Count);
            //
            // if (mutableExporter.DebugImageGrouping.Count != 1091)
            //     Log.Error("Image group count incorrect.  Expected: {0}, Actual: {1}", 1091, mutableExporter.DebugImageGrouping.Count);
            //
            // if (debugImages.Any(image => image.GroupId == -1))
            //     Log.Error("Image is missing group ID");
            //
            // // Current issue is that group 0 is storing 10 images for some reason instead of 4 like it should be.
            // // This also cascades down and makes the rest of the checks fail
            // if (HasInvalidImageGrouping(mutableExporter.DebugImageGrouping[0], 0, 4))
            //     Log.Error("ImageGrouping invalid for Image 0");
            //
            // if (HasInvalidImageGrouping(mutableExporter.DebugImageGrouping[1], 4, 4))
            //     Log.Error("ImageGrouping invalid for Image 1");
            //
            // if (HasInvalidImageGrouping(mutableExporter.DebugImageGrouping[2], 8, 4))
            //     Log.Error("ImageGrouping invalid for Image 2");
            //
            // if (HasInvalidImageGrouping(mutableExporter.DebugImageGrouping[3], 12, 4))
            //     Log.Error("ImageGrouping invalid for Image 3");
            //
            // if (HasInvalidImageGrouping(mutableExporter.DebugImageGrouping[4], 16, 4))
            //     Log.Error("ImageGrouping invalid for Image 4");
            //
            // if (HasInvalidImageGrouping(mutableExporter.DebugImageGrouping[5], 20, 4))
            //     Log.Error("ImageGrouping invalid for Image 5");
            //
            // if (HasInvalidImageGrouping(mutableExporter.DebugImageGrouping[6], 24, 5))
            //     Log.Error("ImageGrouping invalid for Image 6");
        }

        private static void ExportMeshes(List<Tuple<string, Mesh>> meshes)
        {
            foreach (var (path, mesh) in meshes)
            {
                var partName = mesh.FileName.SubstringBeforeLast('.');
                var directory = Path.Combine(Environment.CurrentDirectory, "exports", "meshes", partName);
                var finalPath = $"{directory}.uemodel";
                
                Directory.CreateDirectory(Path.GetDirectoryName(finalPath));
                File.WriteAllBytes(finalPath, mesh.FileData);
            }
        }
        
        private static void ExportMutableImage(CTexture bitmap, int index)
        {
            if (bitmap == null) return;
            try
            {
                var partName = $"{index++:D4}_{bitmap.PixelFormat.ToString()}";
                var directory = Path.Combine(Environment.CurrentDirectory, "exports", "textures", partName);
                var finalPath = $"{directory}.png";
                
                Directory.CreateDirectory(Path.GetDirectoryName(finalPath));
                using var fileStream = File.OpenWrite($"{directory}.png");
                fileStream.Write(bitmap?.Encode(ETextureFormat.Png, false, out _));
            }
            catch (Exception e)
            {
                Log.Error("Image exporting failed for image index {0}", index);
                Log.Error("{0}", e.StackTrace);
            }
        }

        private static bool HasInvalidMeshIdGrouping(List<MutableMeshDef> mutableMeshes, int[] meshIds)
        {
            List<uint> idPrefixes = [];
            foreach (var id in meshIds)
            {
                var mesh = mutableMeshes[id];
                idPrefixes.Add(mesh.Mesh.MeshIDPrefix);
            }

            var first = idPrefixes[0];
            foreach (var prefix in idPrefixes)
            {
                if (prefix != first) return true;
            }

            return false;
        }

        private static bool HasInvalidImageGrouping(List<MutableImageDef> imageGroup, int startIndex, int count)
        {
            if (imageGroup.Count != count) return true;

            var validIDs = Enumerable.Range(startIndex, count).ToArray();
            foreach (var image in imageGroup)
            {
                if (!validIDs.Contains(image.AssetIndex)) return true;
            }

            return false;
        }

        private static ExporterOptions GetExporterOptions()
        {
            return new ExporterOptions()
            {
                MeshFormat = EMeshFormat.UEFormat,
                AnimFormat = EAnimFormat.UEFormat,
                CompressionFormat = EFileCompressionFormat.ZSTD,
                NaniteMeshFormat = ENaniteMeshFormat.OnlyNormalLODs
            };
        }

        private static void InitOodle()
        {
            OodleHelper.DownloadOodleDll();
            OodleHelper.Initialize();
        }
    }
}
