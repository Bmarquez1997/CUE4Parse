using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Image;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Readers;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;

public class FMutableLoader
{
    private UCustomizableObject CustomizableObject { get; set; }
    private UModelStreamableData ModelStreamableData { get; set; }
    private Dictionary<uint, FMutableArchive> SavedArchives { get; set; }

    private Dictionary<uint, FMutableStreamableBlock> ModelStreamables => ModelStreamableData.StreamingData.ModelStreamables;
    private FByteBulkData[] StreamableBulkDataData => ModelStreamableData.StreamingData.StreamableBulkData;

    public FMutableLoader(UCustomizableObject customizableObject)
    {
        if (!customizableObject.Private.TryLoad(out UCustomizableObjectPrivate coPrivate) || !coPrivate.ModelStreamableData.TryLoad(out UModelStreamableData coStreamableData))
            throw new ParserException();

        CustomizableObject = customizableObject;
        ModelStreamableData = coStreamableData;
        SavedArchives = new Dictionary<uint, FMutableArchive>();
    }

    public FMesh LoadMesh(uint index)
    {
        var block = ModelStreamables[index];
        var archive = GetArchive(block);
        archive.Position = (long) block.Offset;
        return new FMesh(archive);
    }

    public FImage LoadImage(uint index)
    {
        var block = ModelStreamables[index];
        var archive = GetArchive(block);
        archive.Position = (long) block.Offset;
        return new FImage(archive);
    }

    private FMutableArchive GetArchive(FMutableStreamableBlock block)
    {
        if (SavedArchives.TryGetValue(block.FileId, out var archive))
            return archive;

        var bulkData = StreamableBulkDataData[block.FileId];
        if (bulkData.Data == null)
            throw new ParserException($"BulkData is null for block: {block.FileId}");

        archive = new FMutableArchive(new FByteArchive($"Mesh Data: {block.FileId}", bulkData.Data));
        SavedArchives[block.FileId] = (FMutableArchive) archive.Clone();

        return archive;
    }
}
