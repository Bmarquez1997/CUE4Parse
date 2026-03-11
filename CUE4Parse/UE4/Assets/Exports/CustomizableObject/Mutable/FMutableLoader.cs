using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Images;
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
        if (customizableObject.Model == null || customizableObject.Private == null 
            || !customizableObject.Private.TryLoad(out var coExport) 
            || (coExport is not UCustomizableObjectPrivate coPrivate) 
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
        // if (block.Flags != 1) return null; HighRes flag
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
        var byteCode = _customizableObject.Model.Program.ByteCode;
        if (byteCode == null) return returnList;
        // In Unreal, EOpType is uint8 (1 byte) per instruction
        foreach (var address in _customizableObject.Model.Program.OpAddress)
        {
            uint offset = MutableByteCode.GetByteCodeOffset(address);
            if (offset >= byteCode.Length) continue;
            returnList.Add((EOpType)byteCode[offset]);
        }
        return returnList;
    }

    /// <summary>
    /// Builds ROM identification by scanning ByteCode for IM_CONSTANT and ME_CONSTANT instructions.
    /// Use this to map each Program.Roms[index] to its logical constant image/mesh and op references.
    /// </summary>
    public RomIdentification? GetRomIdentification()
    {
        if (_customizableObject.Model?.Program == null) return null;
        var byteCode = new MutableByteCode(_customizableObject.Model.Program);
        return byteCode.BuildRomIdentification();
    }
}