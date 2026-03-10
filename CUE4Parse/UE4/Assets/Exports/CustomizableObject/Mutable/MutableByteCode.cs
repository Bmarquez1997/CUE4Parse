using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Roms;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;

/// <summary>
/// Reads and interprets Mutable ByteCode to map ROM indices to their logical constant resources (images/meshes).
/// ByteCode layout matches Unreal Engine Mutable plugin: each instruction is [EOpType (1 byte)][op-specific args].
/// OpAddress entries are FOperation::ADDRESS: low 26 bits = byte offset into ByteCode.
/// </summary>
public class MutableByteCode
{
    /// <summary> Low 26 bits of an ADDRESS are the byte offset into ByteCode. </summary>
    private const uint AddressByteCodeOffsetMask = (1u << 26) - 1;

    /// <summary> Unreal EOpType value for IM_CONSTANT (C# enum order may differ). </summary>
    private const byte UnrealIM_CONSTANT = 34;
    /// <summary> Unreal EOpType value for ME_CONSTANT. </summary>
    private const byte UnrealME_CONSTANT = 44;

    private readonly FProgram _program;
    private readonly byte[] _byteCode;

    public MutableByteCode(FProgram program)
    {
        _program = program ?? throw new ArgumentNullException(nameof(program));
        _byteCode = program.ByteCode ?? Array.Empty<byte>();
    }

    /// <summary>
    /// Gets the byte offset into ByteCode from an FOperation::ADDRESS (from OpAddress array).
    /// </summary>
    public static uint GetByteCodeOffset(uint address)
    {
        return address & AddressByteCodeOffsetMask;
    }

    /// <summary>
    /// Reads the op type at the given byte offset. In Unreal, EOpType is uint8 (1 byte).
    /// </summary>
    public EOpType GetOpTypeAt(uint byteCodeOffset)
    {
        if (byteCodeOffset >= _byteCode.Length)
            return EOpType.NONE;
        return (EOpType)_byteCode[byteCodeOffset];
    }

    /// <summary>
    /// Reads op type and args for an instruction at the given byte offset.
    /// Returns (opType, argsStartOffset). Args start at byteCodeOffset + 1.
    /// </summary>
    public (EOpType opType, uint argsOffset) GetInstructionAt(uint byteCodeOffset)
    {
        EOpType opType = GetOpTypeAt(byteCodeOffset);
        uint argsOffset = byteCodeOffset + 1;
        return (opType, argsOffset);
    }

    /// <summary>
    /// ResourceConstantArgs: single ADDRESS (uint32). Used by IM_CONSTANT (value = ConstantImageIndex).
    /// </summary>
    public static uint ReadResourceConstantArgs(byte[] byteCode, uint argsOffset)
    {
        if (argsOffset + 4 > byteCode.Length) return 0;
        return (uint)(byteCode[argsOffset] | (byteCode[argsOffset + 1] << 8) | (byteCode[argsOffset + 2] << 16) | (byteCode[argsOffset + 3] << 24));
    }

    /// <summary>
    /// MeshConstantArgs: Value (uint32), Skeleton (int32), ClothID (uint32) = 12 bytes.
    /// Value = index into Program.ConstantMeshes.
    /// </summary>
    public static (uint value, int skeleton, uint clothId) ReadMeshConstantArgs(byte[] byteCode, uint argsOffset)
    {
        if (argsOffset + 12 > byteCode.Length)
            return (0, -1, 0);
        uint value = (uint)(byteCode[argsOffset] | (byteCode[argsOffset + 1] << 8) | (byteCode[argsOffset + 2] << 16) | (byteCode[argsOffset + 3] << 24));
        int skeleton = (int)(byteCode[argsOffset + 4] | (byteCode[argsOffset + 5] << 8) | (byteCode[argsOffset + 6] << 16) | (byteCode[argsOffset + 7] << 24));
        uint clothId = (uint)(byteCode[argsOffset + 8] | (byteCode[argsOffset + 9] << 8) | (byteCode[argsOffset + 10] << 16) | (byteCode[argsOffset + 11] << 24));
        return (value, skeleton, clothId);
    }

    /// <summary>
    /// Returns all streamable ROM indices referenced by the given constant image (ConstantImages[index]).
    /// </summary>
    public IReadOnlyList<uint> GetImageConstantRomIndices(int constantImageIndex)
    {
        var list = new List<uint>();
        if (_program.ConstantImages == null || constantImageIndex < 0 || constantImageIndex >= _program.ConstantImages.Length)
            return list;
        if (_program.ConstantImageLODIndices == null)
            return list;

        var lodRange = _program.ConstantImages[constantImageIndex];
        int firstIndex = lodRange.FirstIndex;
        int lodCount = lodRange.LODCount;
        for (int lod = 0; lod < lodCount && firstIndex + lod < _program.ConstantImageLODIndices.Length; lod++)
        {
            var idx = _program.ConstantImageLODIndices[firstIndex + lod];
            if (idx.Streamable)
                list.Add(idx.Index);
        }
        return list;
    }

    /// <summary>
    /// Returns all streamable ROM indices referenced by the given constant mesh (ConstantMeshes[index]).
    /// Order matches runtime: GeometryData, PoseData, PhysicsData, MetaData.
    /// </summary>
    public IReadOnlyList<uint> GetMeshConstantRomIndices(int constantMeshIndex)
    {
        var list = new List<uint>();
        if (_program.ConstantMeshes == null || constantMeshIndex < 0 || constantMeshIndex >= _program.ConstantMeshes.Length)
            return list;
        if (_program.ConstantMeshContentIndices == null)
            return list;

        var range = _program.ConstantMeshes[constantMeshIndex];
        uint firstIndex = range.FirstIndex;
        var flags = range.ContentFlags;
        int count = BitCount((uint)flags);
        for (int i = 0; i < count && firstIndex + i < _program.ConstantMeshContentIndices.Length; i++)
        {
            var idx = _program.ConstantMeshContentIndices[firstIndex + i];
            if (idx.Streamable)
                list.Add(idx.Index);
        }
        return list;
    }

    private static int BitCount(uint n)
    {
        int c = 0;
        while (n != 0) { c += (int)(n & 1); n >>= 1; }
        return c;
    }

    /// <summary>
    /// Enumerates all IM_CONSTANT and ME_CONSTANT instructions in the program and yields
    /// (byteCodeOffset, opType, constantIndex, romIndices) for each. Uses Unreal opcode bytes for detection.
    /// </summary>
    public IEnumerable<(uint byteCodeOffset, EOpType opType, int constantIndex, IReadOnlyList<uint> romIndices)> EnumerateConstantReferences()
    {
        if (_program.OpAddress == null)
            yield break;

        foreach (uint address in _program.OpAddress)
        {
            uint offset = GetByteCodeOffset(address);
            if (offset >= _byteCode.Length) continue;
            byte opByte = _byteCode[offset];
            uint argsOffset = offset + 1;

            if (opByte == UnrealIM_CONSTANT)
            {
                int constantImageIndex = (int)ReadResourceConstantArgs(_byteCode, argsOffset);
                var romIndices = GetImageConstantRomIndices(constantImageIndex);
                yield return (offset, EOpType.IM_CONSTANT, constantImageIndex, romIndices);
            }
            else if (opByte == UnrealME_CONSTANT)
            {
                (uint value, _, _) = ReadMeshConstantArgs(_byteCode, argsOffset);
                int constantMeshIndex = (int)value;
                var romIndices = GetMeshConstantRomIndices(constantMeshIndex);
                yield return (offset, EOpType.ME_CONSTANT, constantMeshIndex, romIndices);
            }
        }
    }

    /// <summary>
    /// Same as EnumerateConstantReferences but for ME_CONSTANT also yields SkeletonConstantIndex (-1 if none).
    /// </summary>
    private IEnumerable<(uint byteCodeOffset, EOpType opType, int constantIndex, IReadOnlyList<uint> romIndices, int skeletonConstantIndex)> EnumerateConstantReferencesWithSkeleton()
    {
        if (_program.OpAddress == null)
            yield break;

        foreach (uint address in _program.OpAddress)
        {
            uint offset = GetByteCodeOffset(address);
            if (offset >= _byteCode.Length) continue;
            byte opByte = _byteCode[offset];
            uint argsOffset = offset + 1;

            if (opByte == UnrealIM_CONSTANT)
            {
                int constantImageIndex = (int)ReadResourceConstantArgs(_byteCode, argsOffset);
                var romIndices = GetImageConstantRomIndices(constantImageIndex);
                yield return (offset, EOpType.IM_CONSTANT, constantImageIndex, romIndices, -1);
            }
            else if (opByte == UnrealME_CONSTANT)
            {
                (uint value, int skeleton, _) = ReadMeshConstantArgs(_byteCode, argsOffset);
                int constantMeshIndex = (int)value;
                var romIndices = GetMeshConstantRomIndices(constantMeshIndex);
                yield return (offset, EOpType.ME_CONSTANT, constantMeshIndex, romIndices, skeleton);
            }
        }
    }

    /// <summary>
    /// Builds a map from ROM index to how it is identified in the ByteCode (type and constant index).
    /// Multiple constant ops can reference the same ROM (e.g. different LODs of same image).
    /// </summary>
    public RomIdentification BuildRomIdentification()
    {
        var byRom = new Dictionary<uint, List<RomRef>>();
        foreach (var (byteCodeOffset, opType, constantIndex, romIndices, skeletonConstantIndex) in EnumerateConstantReferencesWithSkeleton())
        {
            foreach (uint romIndex in romIndices)
            {
                if (!byRom.TryGetValue(romIndex, out var list))
                {
                    list = new List<RomRef>();
                    byRom[romIndex] = list;
                }
                list.Add(new RomRef(opType, constantIndex, byteCodeOffset, skeletonConstantIndex));
            }
        }

        var romType = _program.Roms != null ? (Func<uint, ERomDataType?>)(i => i < _program.Roms.Length ? _program.Roms[i].Type : null) : _ => null;
        return new RomIdentification(_program, byRom, romType, _program.Roms?.Length ?? 0);
    }
}

/// <summary>
/// One reference to a ROM from the ByteCode (an IM_CONSTANT or ME_CONSTANT instruction).
/// For ME_CONSTANT, SkeletonConstantIndex is the index into Program.ConstantSkeletons (-1 if none).
/// </summary>
public readonly struct RomRef
{
    public EOpType OpType { get; }
    public int ConstantIndex { get; }
    public uint ByteCodeOffset { get; }
    /// <summary> Only set for ME_CONSTANT: index into Program.ConstantSkeletons, or -1. </summary>
    public int SkeletonConstantIndex { get; }

    public RomRef(EOpType opType, int constantIndex, uint byteCodeOffset, int skeletonConstantIndex = -1)
    {
        OpType = opType;
        ConstantIndex = constantIndex;
        ByteCodeOffset = byteCodeOffset;
        SkeletonConstantIndex = skeletonConstantIndex;
    }
}

/// <summary>
/// Result of analyzing ByteCode: for each ROM index, the list of constant instructions that reference it.
/// Use GetMeshRomIdentity / GetImageRomIdentity to get logical identity (MeshIDPrefix, skeleton, size/format).
/// </summary>
public class RomIdentification
{
    private readonly FProgram _program;
    private readonly Dictionary<uint, List<RomRef>> _byRom;
    private readonly Func<uint, ERomDataType?> _getRomType;
    private readonly int _romCount;

    internal RomIdentification(FProgram program, Dictionary<uint, List<RomRef>> byRom, Func<uint, ERomDataType?> getRomType, int romCount)
    {
        _program = program;
        _byRom = byRom;
        _getRomType = getRomType;
        _romCount = romCount;
    }

    public int RomCount => _romCount;

    /// <summary> Gets the runtime type of the ROM (Image/Mesh) from Program.Roms, if available. </summary>
    public ERomDataType? GetRomType(uint romIndex) => _getRomType(romIndex);

    /// <summary> Gets all ByteCode references (IM_CONSTANT or ME_CONSTANT) that reference this ROM. </summary>
    public IReadOnlyList<RomRef> GetRefs(uint romIndex)
    {
        return _byRom.TryGetValue(romIndex, out var list) ? list : Array.Empty<RomRef>();
    }

    /// <summary> True if this ROM is referenced by at least one constant instruction. </summary>
    public bool IsReferencedInByteCode(uint romIndex) => _byRom.ContainsKey(romIndex);

    /// <summary> Enumerates all ROM indices that are referenced by IM_CONSTANT or ME_CONSTANT. </summary>
    public IEnumerable<uint> ReferencedRomIndices => _byRom.Keys;

    /// <summary>
    /// Identity for a mesh ROM from ByteCode/Program: MeshIDPrefix, skeleton constant index, and constant mesh index.
    /// SkeletonConstantIndex is -1 if the mesh has no skeleton; otherwise index into Program.ConstantSkeletons.
    /// The streamed FMesh blob also contains MeshIDPrefix when read with the correct constructor alignment.
    /// </summary>
    public MeshRomIdentity? GetMeshRomIdentity(uint romIndex)
    {
        if (!_byRom.TryGetValue(romIndex, out var refs) || refs.Count == 0) return null;
        var r = refs[0];
        if (r.OpType != EOpType.ME_CONSTANT) return null;
        if (_program.ConstantMeshes == null || r.ConstantIndex < 0 || r.ConstantIndex >= _program.ConstantMeshes.Length)
            return null;
        var range = _program.ConstantMeshes[r.ConstantIndex];
        return new MeshRomIdentity(range.MeshIDPrefix, r.SkeletonConstantIndex, r.ConstantIndex);
    }

    /// <summary>
    /// Identity for an image ROM from ByteCode/Program: constant image index and metadata (size, LOD count, format).
    /// Multiple ROMs can belong to the same constant image (one per MIP/LOD).
    /// Use ImageGroupId to group all LODs of the same logical image (same value as ConstantImageIndex; stable across ROMs).
    /// </summary>
    public ImageRomIdentity? GetImageRomIdentity(uint romIndex)
    {
        if (!_byRom.TryGetValue(romIndex, out var refs) || refs.Count == 0) return null;
        var r = refs[0];
        if (r.OpType != EOpType.IM_CONSTANT) return null;
        if (_program.ConstantImages == null || r.ConstantIndex < 0 || r.ConstantIndex >= _program.ConstantImages.Length)
            return null;
        var range = _program.ConstantImages[r.ConstantIndex];
        uint? sourceId = GetRomSourceId(romIndex);
        return new ImageRomIdentity(r.ConstantIndex, range, sourceId);
    }

    /// <summary>
    /// When RomsCompileData is present (e.g. uncooked), returns the compiler's SourceId for this ROM.
    /// ROMs with the same SourceId belong to the same logical resource (all LODs of one image, or one mesh).
    /// Empty in cooked data; returns null when not available.
    /// </summary>
    public uint? GetRomSourceId(uint romIndex)
    {
        if (_program.RomsCompileData == null || romIndex >= _program.RomsCompileData.Length)
            return null;
        return _program.RomsCompileData[romIndex].SourceId;
    }
}

/// <summary> Logical identity of a mesh ROM: from Program.ConstantMeshes and ME_CONSTANT args. </summary>
public readonly struct MeshRomIdentity
{
    public uint MeshIDPrefix { get; }
    public int SkeletonConstantIndex { get; }
    public int ConstantMeshIndex { get; }

    public MeshRomIdentity(uint meshIDPrefix, int skeletonConstantIndex, int constantMeshIndex)
    {
        MeshIDPrefix = meshIDPrefix;
        SkeletonConstantIndex = skeletonConstantIndex;
        ConstantMeshIndex = constantMeshIndex;
    }
}

/// <summary> Logical identity of an image ROM: from Program.ConstantImages. </summary>
/// <remarks>
/// All LODs/MIPs of the same logical image share the same <see cref="ImageGroupId"/> (and <see cref="ConstantImageIndex"/>).
/// Use ImageGroupId to group image ROMs, analogous to MeshIDPrefix for meshes.
/// When RomsCompileData is present, <see cref="RomSourceId"/> gives the compiler's grouping ID (same for all LODs of this image).
/// </remarks>
public readonly struct ImageRomIdentity
{
    public int ConstantImageIndex { get; }
    public Images.FImageLODRange Metadata { get; }

    /// <summary>
    /// Stable ID to group all LODs/MIPs of the same logical image. Same value across every ROM that belongs to this constant image.
    /// Use this to group image ROMs (e.g. for naming or bundling). Equals ConstantImageIndex as uint.
    /// </summary>
    public uint ImageGroupId => (uint)ConstantImageIndex;

    /// <summary>
    /// Compiler SourceId when RomsCompileData is present (e.g. uncooked); null when cooked.
    /// ROMs with the same RomSourceId belong to the same logical resource.
    /// </summary>
    public uint? RomSourceId { get; }

    public ImageRomIdentity(int constantImageIndex, Images.FImageLODRange metadata, uint? romSourceId = null)
    {
        ConstantImageIndex = constantImageIndex;
        Metadata = metadata;
        RomSourceId = romSourceId;
    }
}
