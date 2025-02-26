using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Readers;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;

public class FMutableLoader
{
    private UCustomizableObject CustomizableObject { get; set; }
    private UModelStreamableData ModelStreamableData { get; set; }
    private Dictionary<uint, FByteArchive> SavedArchives { get; set; }

    private Dictionary<uint, FMutableStreamableBlock> ModelStreamables => ModelStreamableData.StreamingData.ModelStreamables;
    private FByteBulkData[] StreamableBulkDataData => ModelStreamableData.StreamingData.StreamableBulkData;
    //public FProgram Program => CustomizableObject.Model.Program;

    public FMutableLoader(UCustomizableObject customizableObject)
    {
        if (!customizableObject.Private.TryLoad(out UCustomizableObjectPrivate coPrivate) || !coPrivate.ModelStreamableData.TryLoad(out UModelStreamableData coStreamableData))
            throw new ParserException();

        CustomizableObject = customizableObject;
        ModelStreamableData = coStreamableData;
        SavedArchives = new Dictionary<uint, FByteArchive>();
    }

    public FMesh LoadMesh(uint index)
    {
        var block = ModelStreamables[index];
        if (!SavedArchives.TryGetValue(block.FileId, out var archive))
        {
            var bulkData = StreamableBulkDataData[block.FileId];
            if (bulkData.Data == null)
                throw new ParserException($"Bulkdata for block: {block.FileId} is null");

            archive = new FByteArchive($"Mesh Data: {block.FileId}", bulkData.Data);
            SavedArchives[block.FileId] = (FByteArchive) archive.Clone();
        }

        archive.Position = (long) block.Offset;
        return new FMesh(archive);
    }
}
