using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Image;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Readers;
using Serilog;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;

public class FMutableLoader
{
    private readonly UCustomizableObject _customizableObject;
    private readonly UModelStreamableData _modelStreamableData;
    private readonly Dictionary<uint, FMutableArchive> _loadedArchives;

    private Dictionary<uint, FMutableStreamableBlock> ModelStreamables => _modelStreamableData.StreamingData.ModelStreamables;
    private FByteBulkData[] StreamableBulkDataData => _modelStreamableData.StreamingData.StreamableBulkData;

    public FMutableLoader(UCustomizableObject customizableObject)
    {
        if (!customizableObject.Private.TryLoad(out var coExport) || (coExport is not UCustomizableObjectPrivate coPrivate) 
            || !coPrivate.ModelStreamableData.TryLoad(out var coStreamableData))
            throw new ParserException();

        _customizableObject = customizableObject;
        _modelStreamableData = coStreamableData as UModelStreamableData ?? throw new Exception();
        _loadedArchives = new Dictionary<uint, FMutableArchive>();
    }

    public FMesh LoadMesh(uint index)
    {
        var block = ModelStreamables[index];
        var archive = GetArchive(block);
        archive.Position = (long) block.Offset;
        return new FMesh(archive);
    }

    public FImage? LoadImage(uint index)
    {
        var block = ModelStreamables[index];
        if (block.Flags != 1) return null;
        var archive = GetArchive(block);
        archive.Position = (long) block.Offset;
        return new FImage(archive);
    }

    private FMutableArchive GetArchive(FMutableStreamableBlock block)
    {
        if (_loadedArchives.TryGetValue(block.FileId, out var archive))
            return archive;

        var bulkData = StreamableBulkDataData[block.FileId];
        if (bulkData.Data == null) throw new ParserException($"BulkData is null for block: {block.FileId}");

        archive = new FMutableArchive(new FByteArchive($"Mesh Data: {block.FileId}", bulkData.Data));
        _loadedArchives[block.FileId] = (FMutableArchive) archive.Clone();

        return archive;
    }
    
    public List<EOpType> ReadByteCode()
    {
        List<EOpType> returnList = [];
        if (_customizableObject.Model == null) return returnList;
        var bytecodeReader = new FByteArchive("Mutable ByteCode", _customizableObject.Model.Program.ByteCode);
        foreach (var address in _customizableObject.Model.Program.OpAddress)
        {
            bytecodeReader.Position = address;

            var opCodeType = bytecodeReader.Read<EOpType>();
            returnList.Add(opCodeType);
        }

        return returnList;
    }
}