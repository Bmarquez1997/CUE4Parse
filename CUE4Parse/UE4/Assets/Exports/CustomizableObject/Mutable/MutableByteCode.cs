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
    
    /// <summary> Sentinel in OpArgsSizeTable for variable-length op args (SWITCH). </summary>
    private const int OpArgsSizeVariable = 255;

    /// <summary> Unreal EOpType value for IM_CONSTANT (C# enum order may differ). </summary>
    private const byte UnrealIM_CONSTANT = 34;
    /// <summary> Unreal EOpType value for ME_CONSTANT. </summary>
    private const byte UnrealME_CONSTANT = 44;

    /// <summary> Unreal EOpType values for SWITCH ops (variable-length args: VarAddress, DefAddress, FSwitchCaseDescriptor, then Count cases). </summary>
    private static readonly HashSet<byte> SwitchOpCodes = [10, 14, 22, 38, 48, 63, 76, 84, 93, 96];

    /// <summary>
    /// Args size in bytes per op type (Unreal EOpType value). 0 = no args; 255 = variable (SWITCH).
    /// Derived from Unreal Operations.h struct sizes. Unknown ops use 4 as safe default.
    /// </summary>
    private static readonly byte[] OpArgsSizeTable = BuildOpArgsSizeTable();

    private static byte[] BuildOpArgsSizeTable()
    {
        var t = new byte[256];
        for (int i = 0; i < t.Length; i++)
            t[i] = 4; // safe default (many ops have at least one ADDRESS)
        t[0] = 0;   // NONE
        t[1] = 4;   // BO_CONSTANT (BoolConstantArgs)
        t[2] = 4;   // BO_PARAMETER
        t[3] = 8;   // BO_EQUAL_INT_CONST
        t[4] = 8;   // BO_AND
        t[5] = 8;   // BO_OR
        t[6] = 4;   // BO_NOT
        t[7] = 4;   // NU_CONSTANT
        t[8] = 4;   // NU_PARAMETER
        t[9] = 12;  // NU_CONDITIONAL
        t[10] = OpArgsSizeVariable;  // NU_SWITCH
        t[11] = 4;   // SC_CONSTANT
        t[12] = 4;   // SC_PARAMETER
        t[13] = 12;  // SC_CONDITIONAL
        t[14] = OpArgsSizeVariable;  // SC_SWITCH
        t[15] = 4;   // SC_MATERIAL_BREAK
        t[16] = 8;   // SC_ARITHMETIC
        t[17] = 8;   // SC_CURVE
        t[18] = 8;   // SC_EXTERNAL (ExternalArgs minimal)
        t[19] = 16; // CO_CONSTANT (FVector4f)
        t[20] = 4;   // CO_PARAMETER
        t[21] = 12;  // CO_CONDITIONAL
        t[22] = OpArgsSizeVariable;  // CO_SWITCH
        t[23] = 4;   // CO_MATERIAL_BREAK
        t[24] = 12; // CO_SAMPLEIMAGE
        t[25] = 16; // CO_SWIZZLE
        t[26] = 16; // CO_FROMSCALARS
        t[27] = 8;   // CO_ARITHMETIC
        t[28] = 4;   // CO_LINEARTOSRGB
        t[29] = 8;   // CO_EXTERNAL
        t[30] = 4;   // ST_CONSTANT
        t[31] = 4;   // ST_PARAMETER
        t[32] = 4;   // PR_CONSTANT (minimal)
        t[33] = 4;   // PR_PARAMETER
        t[34] = 4;   // IM_CONSTANT (ResourceConstantArgs)
        t[35] = 4;   // IM_PARAMETER
        t[36] = 12;  // IM_REFERENCE (ResourceReferenceArgs: FImageDesc + ID + int8)
        t[37] = 12;  // IM_CONDITIONAL
        t[38] = OpArgsSizeVariable;  // IM_SWITCH
        t[39] = 4;   // IM_MATERIAL_BREAK
        t[40] = 4;   // IM_PARAMETER_FROM_MATERIAL
        t[41] = 8;   // IM_LAYER
        t[42] = 8;   // IM_LAYERCOLOUR
        t[43] = 4;   // IM_PIXELFORMAT
        t[44] = 12;  // ME_CONSTANT (MeshConstantArgs)
        t[45] = 4;   // ME_PARAMETER
        t[46] = 12;  // ME_REFERENCE
        t[47] = 12;  // ME_CONDITIONAL
        t[48] = OpArgsSizeVariable;  // ME_SWITCH
        t[49] = 8;   // ME_APPLYLAYOUT
        t[50] = 8;   // ME_PREPARELAYOUT
        t[51] = 8;   // ME_DIFFERENCE
        t[52] = 8;   // ME_MORPH
        t[53] = 8;   // ME_MERGE
        t[48] = OpArgsSizeVariable;  // ME_SWITCH
        t[49] = 8;   // ME_APPLYLAYOUT
        t[50] = 8;   // ME_PREPARELAYOUT
        t[51] = 8;   // ME_DIFFERENCE
        t[52] = 8;   // ME_MORPH
        t[53] = 8;   // ME_MERGE
        t[54] = 8;   // ME_MASKCLIPMESH
        t[55] = 8;   // ME_MASKCLIPUVMASK
        t[56] = 44;  // ME_ADDMETADATA (MeshAddMetadataArgs)
        t[57] = 12;  // ME_TRANSFORMWITHMESH (approx)
        t[58] = 12;  // ME_TRANSFORMWITHBONE (approx)
        t[59] = 8;   // ME_EXTERNAL
        t[60] = 8;   // ME_SKELETALMESH_BREAK
        t[61] = 4;   // SK_PARAMETER
        t[62] = 12;  // IN_CONDITIONAL
        t[63] = OpArgsSizeVariable;  // IN_SWITCH
        t[64] = 8;   // IN_ADDMESH (InstanceAddArgs)
        t[65] = 8;   // IN_ADDSTRING
        t[66] = 8;   // IN_ADDSURFACE
        t[67] = 8;   // IN_ADDCOMPONENT
        t[68] = 8;   // IN_ADDLOD
        t[69] = 8;   // IN_ADDSKELETALMESH
        t[70] = 8;   // IN_ADDEXTENSIONDATA
        t[71] = 8;   // IN_ADDOVERLAYMATERIAL
        t[72] = 8;   // IN_ADDOVERRIDEMATERIAL
        t[73] = 8;   // IN_ADDMATERIAL
        t[74] = 4;   // LA_CONSTANT
        t[75] = 12;  // LA_CONDITIONAL
        t[76] = OpArgsSizeVariable;  // LA_SWITCH
        t[77] = 8;   // LA_PACK
        t[78] = 8;   // LA_MERGE
        t[79] = 8;   // LA_REMOVEBLOCKS
        t[80] = 8;   // LA_FROMMESH
        t[81] = 4;   // MI_CONSTANT
        t[82] = 4;   // MI_PARAMETER
        t[83] = 12;  // MI_CONDITIONAL
        t[84] = OpArgsSizeVariable;  // MI_SWITCH
        t[85] = 8;   // MI_SKELETALMESH_BREAK
        t[86] = 8;   // MI_FROM_SKELETALMESH_SLOT
        t[87] = 4;   // MI_MODIFY (variable in Unreal; use min)
        t[88] = 8;   // MI_EXTERNAL
        t[89] = 4;   // MA_CONSTANT
        t[90] = 4;   // MA_PARAMETER
        t[91] = 4;   // ED_CONSTANT
        t[92] = 12;  // ED_CONDITIONAL
        t[93] = OpArgsSizeVariable;  // ED_SWITCH
        t[94] = 4;   // IS_PARAMETER
        t[95] = 8;   // IS_EXTERNAL
        t[96] = OpArgsSizeVariable;  // IS_SWITCH
        return t;
    }

    /// <summary>
    /// Computes the args size in bytes for a variable-length SWITCH op. Layout: VarAddress(4), DefAddress(4),
    /// FSwitchCaseDescriptor(4: Count in low 31 bits, bUseRanges in bit 31), then Count*(Condition(4)+Address(4)) or Count*(Start(4)+Size(4)+Address(4)).
    /// </summary>
    private static int GetSwitchOpArgsSize(byte[] byteCode, int argsOffset)
    {
        if (argsOffset + 12 > byteCode.Length) return 0;
        uint caseDesc = (uint)(byteCode[argsOffset + 8] | (byteCode[argsOffset + 9] << 8) | (byteCode[argsOffset + 10] << 16) | (byteCode[argsOffset + 11] << 24));
        int count = (int)(caseDesc & 0x7FFFFFFF);
        bool useRanges = (caseDesc & 0x80000000) != 0;
        int caseSize = useRanges ? 12 : 8; // (int32+uint32+ADDRESS) or (int32+ADDRESS)
        return 12 + count * caseSize;
    }

    /// <summary>
    /// Builds the OpAddress array by scanning ByteCode. Each instruction is [op (1 byte)][args].
    /// Unreal does not serialize OpAddress; this reconstructs it so ROM identification works.
    /// </summary>
    public static uint[] BuildOpAddressFromByteCode(byte[]? byteCode)
    {
        if (byteCode == null || byteCode.Length == 0)
            return [];
        var list = new List<uint>();
        int offset = 0;
        while (offset < byteCode.Length)
        {
            list.Add((uint)offset);
            byte op = byteCode[offset];
            int argsSize = offset + 1 < byteCode.Length ? GetOpArgsSize(byteCode, offset + 1, op) : 0;
            offset += 1 + argsSize;
        }
        return list.ToArray();
    }

    private static int GetOpArgsSize(byte[] byteCode, int argsOffset, byte opType)
    {
        if (opType >= OpArgsSizeTable.Length)
            return 4;
        int size = OpArgsSizeTable[opType];
        if (size == OpArgsSizeVariable && SwitchOpCodes.Contains(opType))
            return GetSwitchOpArgsSize(byteCode, argsOffset);
        return size;
    }

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

            if (opByte == (byte)EOpType.IM_CONSTANT)
            {
                int constantImageIndex = (int)ReadResourceConstantArgs(_byteCode, argsOffset);
                var romIndices = GetImageConstantRomIndices(constantImageIndex);
                yield return (offset, EOpType.IM_CONSTANT, constantImageIndex, romIndices);
            }
            else if (opByte == (byte)EOpType.ME_CONSTANT)
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

            if (opByte == (byte)EOpType.IM_CONSTANT)
            {
                int constantImageIndex = (int)ReadResourceConstantArgs(_byteCode, argsOffset);
                var romIndices = GetImageConstantRomIndices(constantImageIndex);
                yield return (offset, EOpType.IM_CONSTANT, constantImageIndex, romIndices, -1);
            }
            else if (opByte == (byte)EOpType.ME_CONSTANT)
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
