using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh;
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
    /// When the value is an op index (into OpAddress), use OpIndexToByteCodeOffset instead.
    /// </summary>
    public static uint GetByteCodeOffset(uint address)
    {
        return address & AddressByteCodeOffsetMask;
    }

    /// <summary>
    /// Finds the op index (into Program.OpAddress) whose byte offset equals the given byteCodeOffset.
    /// Returns null if not found.
    /// </summary>
    public uint? GetOpIndexFromByteCodeOffset(uint byteCodeOffset)
    {
        if (_program.OpAddress == null) return null;
        uint target = byteCodeOffset & AddressByteCodeOffsetMask;
        for (int i = 0; i < _program.OpAddress.Length; i++)
        {
            if ((_program.OpAddress[i] & AddressByteCodeOffsetMask) == target)
                return (uint)i;
        }
        return null;
    }

    /// <summary>
    /// Converts an op index (into Program.OpAddress) to the byte offset of that op.
    /// Unreal stores ADDRESS in bytecode as op index; OpAddress[index] = byte offset.
    /// </summary>
    public uint OpIndexToByteCodeOffset(uint opIndex)
    {
        if (_program.OpAddress == null || opIndex >= _program.OpAddress.Length)
            return 0;
        return _program.OpAddress[opIndex] & AddressByteCodeOffsetMask;
    }

    /// <summary>
    /// Resolves an ADDRESS from bytecode: if it looks like an op index (valid index into OpAddress), return that op's byte offset; otherwise treat as raw byte offset.
    /// Some assets store ADDRESS as (opIndex &lt;&lt; 8) or (opIndex &lt;&lt; 16); when the raw value is past ByteCode length, we try &gt;&gt; 8 then &gt;&gt; 16 as op index.
    /// </summary>
    public uint AddressToByteCodeOffset(uint address)
    {
        if (_program.OpAddress != null && address < (uint)_program.OpAddress.Length)
            return OpIndexToByteCodeOffset(address);
        uint len = (uint)(_byteCode?.Length ?? 0);
        if (_program.OpAddress != null && address >= len)
        {
            uint shifted8 = address >> 8;
            if (shifted8 < (uint)_program.OpAddress.Length)
                return OpIndexToByteCodeOffset(shifted8);
            uint shifted16 = address >> 16;
            if (shifted16 < (uint)_program.OpAddress.Length)
                return OpIndexToByteCodeOffset(shifted16);
        }
        return GetByteCodeOffset(address);
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

    /// <summary> ParameterArgs: variable (ADDRESS = parameter index into Program.Parameters). Used by NU_PARAMETER. </summary>
    public static uint ReadParameterArgs(byte[] byteCode, uint argsOffset)
    {
        if (argsOffset + 4 > byteCode.Length) return 0;
        return (uint)(byteCode[argsOffset] | (byteCode[argsOffset + 1] << 8) | (byteCode[argsOffset + 2] << 16) | (byteCode[argsOffset + 3] << 24));
    }

    /// <summary> InstanceAddArgs first two fields: instance (ADDRESS), value (ADDRESS) = 8 bytes. Used by IN_ADDMESH, IN_ADDIMAGE, IN_ADDSURFACE. </summary>
    public static (uint instance, uint value) ReadInstanceAddArgs(byte[] byteCode, uint argsOffset)
    {
        if (argsOffset + 8 > byteCode.Length) return (0, 0);
        uint instance = (uint)(byteCode[argsOffset] | (byteCode[argsOffset + 1] << 8) | (byteCode[argsOffset + 2] << 16) | (byteCode[argsOffset + 3] << 24));
        uint value = (uint)(byteCode[argsOffset + 4] | (byteCode[argsOffset + 5] << 8) | (byteCode[argsOffset + 6] << 16) | (byteCode[argsOffset + 7] << 24));
        return (instance, value);
    }

    /// <summary> ConditionalArgs: condition (4), yes (4), no (4) = 12 bytes. </summary>
    public static (uint condition, uint yes, uint no) ReadConditionalArgs(byte[] byteCode, uint argsOffset)
    {
        if (argsOffset + 12 > byteCode.Length) return (0, 0, 0);
        uint c = (uint)(byteCode[argsOffset] | (byteCode[argsOffset + 1] << 8) | (byteCode[argsOffset + 2] << 16) | (byteCode[argsOffset + 3] << 24));
        uint y = (uint)(byteCode[argsOffset + 4] | (byteCode[argsOffset + 5] << 8) | (byteCode[argsOffset + 6] << 16) | (byteCode[argsOffset + 7] << 24));
        uint n = (uint)(byteCode[argsOffset + 8] | (byteCode[argsOffset + 9] << 8) | (byteCode[argsOffset + 10] << 16) | (byteCode[argsOffset + 11] << 24));
        return (c, y, n);
    }

    /// <summary>
    /// Reads SWITCH op args: VarAddress (4), DefAddress (4), FSwitchCaseDescriptor (4: Count in low 31 bits, bUseRanges in bit 31),
    /// then Count times (Condition int32 + CaseAt ADDRESS) or (Start+Size+CaseAt) if bUseRanges.
    /// Returns (varAddress, defAddress, useRanges, list of (conditionOrStart, sizeOr0, caseAt)).
    /// </summary>
    public static (uint varAddress, uint defAddress, bool useRanges, List<(int conditionOrStart, int size, uint caseAt)>) ReadSwitchOpArgs(byte[] byteCode, uint argsOffset)
    {
        var cases = new List<(int, int, uint)>();
        if (argsOffset + 12 > byteCode.Length) return (0, 0, false, cases);
        uint varAddress = (uint)(byteCode[argsOffset] | (byteCode[argsOffset + 1] << 8) | (byteCode[argsOffset + 2] << 16) | (byteCode[argsOffset + 3] << 24));
        uint defAddress = (uint)(byteCode[argsOffset + 4] | (byteCode[argsOffset + 5] << 8) | (byteCode[argsOffset + 6] << 16) | (byteCode[argsOffset + 7] << 24));
        uint caseDesc = (uint)(byteCode[argsOffset + 8] | (byteCode[argsOffset + 9] << 8) | (byteCode[argsOffset + 10] << 16) | (byteCode[argsOffset + 11] << 24));
        int count = (int)(caseDesc & 0x7FFFFFFF);
        bool useRanges = (caseDesc & 0x80000000) != 0;
        int pos = (int)(argsOffset + 12);
        int caseSize = useRanges ? 12 : 8;
        for (int i = 0; i < count && pos + caseSize <= byteCode.Length; i++)
        {
            int c = (int)(byteCode[pos] | (byteCode[pos + 1] << 8) | (byteCode[pos + 2] << 16) | (byteCode[pos + 3] << 24));
            pos += 4;
            int size = 0;
            if (useRanges)
            {
                size = (int)(byteCode[pos] | (byteCode[pos + 1] << 8) | (byteCode[pos + 2] << 16) | (byteCode[pos + 3] << 24));
                pos += 4;
            }
            uint caseAt = (uint)(byteCode[pos] | (byteCode[pos + 1] << 8) | (byteCode[pos + 2] << 16) | (byteCode[pos + 3] << 24));
            pos += 4;
            cases.Add((c, size, caseAt));
        }
        return (varAddress, defAddress, useRanges, cases);
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
    /// Finds the constant mesh index and range for a ROM index by scanning Program.ConstantMeshes and ConstantMeshContentIndices.
    /// Used when the ROM is not referenced by any ME_CONSTANT in ByteCode (e.g. only referenced via ME_SWITCH).
    /// </summary>
    public (int constantMeshIndex, FMeshContentRange range)? TryGetMeshConstantForRomIndex(uint romIndex)
    {
        if (_program.ConstantMeshes == null || _program.ConstantMeshContentIndices == null)
            return null;
        for (int c = 0; c < _program.ConstantMeshes.Length; c++)
        {
            var romIndices = GetMeshConstantRomIndices(c);
            foreach (var idx in romIndices)
            {
                if (idx == romIndex)
                    return (c, _program.ConstantMeshes[c]);
            }
        }
        return null;
    }

    /// <summary>
    /// Finds the constant image index and range for a ROM index by scanning Program.ConstantImages and ConstantImageLODIndices.
    /// Used when the ROM is not referenced by any IM_CONSTANT in ByteCode (e.g. only referenced via IM_SWITCH).
    /// </summary>
    public (int constantImageIndex, Images.FImageLODRange range)? TryGetImageConstantForRomIndex(uint romIndex)
    {
        if (_program.ConstantImages == null || _program.ConstantImageLODIndices == null)
            return null;
        for (int c = 0; c < _program.ConstantImages.Length; c++)
        {
            var romIndices = GetImageConstantRomIndices(c);
            foreach (var idx in romIndices)
            {
                if (idx == romIndex)
                    return (c, _program.ConstantImages[c]);
            }
        }
        return null;
    }

    /// <summary>
    /// Resolves a SWITCH variable address to the parameter index (into Program.Parameters).
    /// VarAddress is the ADDRESS of the op that produces the int variable (usually NU_PARAMETER).
    /// Uses low 26 bits as byte offset into ByteCode per UE convention.
    /// </summary>
    public int? ResolveVarAddressToParameterIndex(uint varAddress)
    {
        uint offset = AddressToByteCodeOffset(varAddress);
        if (offset == 0 || offset >= _byteCode.Length) return null;
        byte op = _byteCode[offset];
        if (op == (byte)EOpType.NU_PARAMETER)
        {
            uint argsOffset = offset + 1;
            if (argsOffset + 4 <= _byteCode.Length)
            {
                uint variable = ReadParameterArgs(_byteCode, argsOffset);
                if (variable < (_program.Parameters?.Length ?? 0))
                    return (int)variable;
            }
            return null;
        }
        // VarAddress may be stored as (opIndex << 8) or (opIndex << 16); try resolving as op index
        if (_program.OpAddress != null)
        {
            uint shifted8 = varAddress >> 8;
            if (shifted8 < (uint)_program.OpAddress.Length)
            {
                uint off8 = OpIndexToByteCodeOffset(shifted8);
                if (off8 < _byteCode.Length && _byteCode[off8] == (byte)EOpType.NU_PARAMETER)
                {
                    uint argsOffset = off8 + 1;
                    if (argsOffset + 4 <= _byteCode.Length)
                    {
                        uint variable = ReadParameterArgs(_byteCode, argsOffset);
                        if (variable < (_program.Parameters?.Length ?? 0))
                            return (int)variable;
                    }
                }
            }
            uint shifted16 = varAddress >> 16;
            if (shifted16 < (uint)_program.OpAddress.Length)
            {
                uint off16 = OpIndexToByteCodeOffset(shifted16);
                if (off16 < _byteCode.Length && _byteCode[off16] == (byte)EOpType.NU_PARAMETER)
                {
                    uint argsOffset = off16 + 1;
                    if (argsOffset + 4 <= _byteCode.Length)
                    {
                        uint variable = ReadParameterArgs(_byteCode, argsOffset);
                        if (variable < (_program.Parameters?.Length ?? 0))
                            return (int)variable;
                    }
                }
            }
        }
        // Some assets store VarAddress as the parameter index directly (when it fits)
        if (varAddress < (_program.Parameters?.Length ?? 0))
            return (int)varAddress;
        return null;
    }

    /// <summary>
    /// Collects image and mesh constant indices reachable from the given op address when following
    /// CONDITIONAL (both branches) and SWITCH (only the case matching the given parameter option index).
    /// Address is byte offset into ByteCode (use GetByteCodeOffset if you have an ADDRESS).
    /// paramIndexToOptionIndex: parameter index -> option index (0-based) for SWITCH selection.
    /// constLogDepth: when >= 0 and &lt;= 15 and LogPath set, log CONST_* lines for debugging (-1 = no log).
    /// </summary>
    public (HashSet<int> imageConstants, HashSet<int> meshConstants) CollectConstantsFromAddress(
        uint byteCodeOffset,
        IReadOnlyDictionary<int, int> paramIndexToOptionIndex,
        HashSet<uint>? visited = null,
        int constLogDepth = -1)
    {
        var images = new HashSet<int>();
        var meshes = new HashSet<int>();
        visited ??= [];
        if (!visited.Add(byteCodeOffset)) return (images, meshes);

        if (byteCodeOffset >= _byteCode.Length) return (images, meshes);
        var (opType, argsOffset) = GetInstructionAt(byteCodeOffset);

        bool doLog = constLogDepth >= 0 && constLogDepth <= 15 && !string.IsNullOrEmpty(MutableResolverDebugLog.LogPath);
        if (doLog) MutableResolverDebugLog.Log($"CONST_OP\tdepth={constLogDepth}\toffset={byteCodeOffset}\topType={opType}");

        switch (opType)
        {
            case EOpType.IM_CONSTANT:
                uint ciRaw = ReadResourceConstantArgs(_byteCode, argsOffset);
                int imgConstLen = _program.ConstantImages?.Length ?? 0;
                int ci = (int)ciRaw;
                if (ci >= imgConstLen && imgConstLen > 0)
                {
                    int try8 = (int)(ciRaw >> 8), try16 = (int)(ciRaw >> 16);
                    if (try8 >= 0 && try8 < imgConstLen) ci = try8;
                    else if (try16 >= 0 && try16 < imgConstLen) ci = try16;
                }
                if (ci >= 0 && ci < imgConstLen) { images.Add(ci); if (doLog) MutableResolverDebugLog.Log($"CONST_ADD_IMAGE\tdepth={constLogDepth}\tconstantIndex={ci}"); }
                break;
            case EOpType.ME_CONSTANT:
                var (mc, _, _) = ReadMeshConstantArgs(_byteCode, argsOffset);
                int meshConstLen = _program.ConstantMeshes?.Length ?? 0;
                if (doLog && (mc >= (uint)meshConstLen)) MutableResolverDebugLog.Log($"CONST_ME_CONSTANT\tdepth={constLogDepth}\tvalue={mc}\tconstantMeshesLen={meshConstLen}");
                int meshIndex = (int)mc;
                if (meshIndex >= meshConstLen && meshConstLen > 0)
                {
                    // Value may be stored as op index (e.g. value >> 8); use as constant index if in range
                    int try8 = (int)(mc >> 8), try16 = (int)(mc >> 16);
                    if (try8 >= 0 && try8 < meshConstLen) meshIndex = try8;
                    else if (try16 >= 0 && try16 < meshConstLen) meshIndex = try16;
                }
                if (meshIndex >= 0 && meshIndex < meshConstLen) { meshes.Add(meshIndex); if (doLog) MutableResolverDebugLog.Log($"CONST_ADD_MESH\tdepth={constLogDepth}\tconstantIndex={meshIndex}"); }
                break;
            case EOpType.IM_CONDITIONAL:
            case EOpType.ME_CONDITIONAL:
                var (_, yes, no) = ReadConditionalArgs(_byteCode, argsOffset);
                uint yesOff = AddressToByteCodeOffset(yes), noOff = AddressToByteCodeOffset(no);
                if (doLog) MutableResolverDebugLog.Log($"CONST_CONDITIONAL\tdepth={constLogDepth}\tyes={yes}\tyesOff={yesOff}\tno={no}\tnoOff={noOff}");
                if (yesOff != 0) { var (yi, ym) = CollectConstantsFromAddress(yesOff, paramIndexToOptionIndex, visited, constLogDepth >= 0 ? constLogDepth + 1 : -1); foreach (var i in yi) images.Add(i); foreach (var m in ym) meshes.Add(m); }
                if (noOff != 0) { var (ni, nm) = CollectConstantsFromAddress(noOff, paramIndexToOptionIndex, visited, constLogDepth >= 0 ? constLogDepth + 1 : -1); foreach (var i in ni) images.Add(i); foreach (var m in nm) meshes.Add(m); }
                break;
            case EOpType.IM_SWITCH:
            case EOpType.ME_SWITCH:
                var (varAddress, defAddress, useRanges, cases) = ReadSwitchOpArgs(_byteCode, argsOffset);
                int? paramIndex = ResolveVarAddressToParameterIndex(varAddress);
                int? optionIndex = paramIndex.HasValue && paramIndexToOptionIndex.TryGetValue(paramIndex.Value, out var oi) ? oi : null;
                uint varResolvedOff = AddressToByteCodeOffset(varAddress);
                if (doLog) MutableResolverDebugLog.Log($"CONST_SWITCH\tdepth={constLogDepth}\tvarAddress={varAddress}\tvarResolvedOff={varResolvedOff}\tresolvedParamIndex={paramIndex?.ToString() ?? "null"}\toptionIndex={optionIndex?.ToString() ?? "null"}\tcasesCount={cases.Count}\tuseRanges={useRanges}");
                bool switchMatched = false;
                if (optionIndex.HasValue)
                {
                    foreach (var (c, size, caseAt) in cases)
                    {
                        uint caseOff = AddressToByteCodeOffset((uint)caseAt);
                        bool match = caseOff != 0 && (useRanges ? (optionIndex.Value >= c && optionIndex.Value < c + size) : (c == optionIndex.Value));
                        if (doLog) MutableResolverDebugLog.Log($"CONST_SWITCH_CASE\tdepth={constLogDepth}\tcondition={c}\tsize={size}\tcaseAt={caseAt}\tcaseOff={caseOff}\tmatch={match}");
                        if (match) { switchMatched = true; var (si, sm) = CollectConstantsFromAddress(caseOff, paramIndexToOptionIndex, visited, constLogDepth >= 0 ? constLogDepth + 1 : -1); foreach (var i in si) images.Add(i); foreach (var m in sm) meshes.Add(m); break; }
                    }
                    if (doLog && !switchMatched) MutableResolverDebugLog.Log($"CONST_SWITCH_NO_MATCH\tdepth={constLogDepth}");
                }
                else if (doLog) MutableResolverDebugLog.Log($"CONST_SWITCH_NO_OPTION\tdepth={constLogDepth}");
                // When no case matched (or no option), follow default branch to still collect constants
                if (!switchMatched && defAddress != 0)
                {
                    uint defOff = AddressToByteCodeOffset(defAddress);
                    if (defOff != 0) { var (di, dm) = CollectConstantsFromAddress(defOff, paramIndexToOptionIndex, visited, constLogDepth >= 0 ? constLogDepth + 1 : -1); foreach (var i in di) images.Add(i); foreach (var m in dm) meshes.Add(m); }
                }
                break;
            case EOpType.IM_LAYER:
                if (argsOffset + 12 <= _byteCode.Length)
                {
                    uint b = (uint)(_byteCode[argsOffset] | (_byteCode[argsOffset + 1] << 8) | (_byteCode[argsOffset + 2] << 16) | (_byteCode[argsOffset + 3] << 24));
                    uint mask = (uint)(_byteCode[argsOffset + 4] | (_byteCode[argsOffset + 5] << 8) | (_byteCode[argsOffset + 6] << 16) | (_byteCode[argsOffset + 7] << 24));
                    uint blend = (uint)(_byteCode[argsOffset + 8] | (_byteCode[argsOffset + 9] << 8) | (_byteCode[argsOffset + 10] << 16) | (_byteCode[argsOffset + 11] << 24));
                    foreach (uint a in new[] { b, mask, blend }) { uint o = AddressToByteCodeOffset(a); if (o != 0) { var (xi, xm) = CollectConstantsFromAddress(o, paramIndexToOptionIndex, visited, constLogDepth >= 0 ? constLogDepth + 1 : -1); foreach (var i in xi) images.Add(i); foreach (var m in xm) meshes.Add(m); } }
                }
                break;
            case EOpType.ME_MERGE:
                if (argsOffset + 8 <= _byteCode.Length)
                {
                    uint baseAddr = (uint)(_byteCode[argsOffset] | (_byteCode[argsOffset + 1] << 8) | (_byteCode[argsOffset + 2] << 16) | (_byteCode[argsOffset + 3] << 24));
                    uint addedAddr = (uint)(_byteCode[argsOffset + 4] | (_byteCode[argsOffset + 5] << 8) | (_byteCode[argsOffset + 6] << 16) | (_byteCode[argsOffset + 7] << 24));
                    foreach (uint a in new[] { baseAddr, addedAddr }) { uint o = AddressToByteCodeOffset(a); if (o != 0) { var (xi, xm) = CollectConstantsFromAddress(o, paramIndexToOptionIndex, visited, constLogDepth >= 0 ? constLogDepth + 1 : -1); foreach (var i in xi) images.Add(i); foreach (var m in xm) meshes.Add(m); } }
                }
                break;
            case EOpType.ME_PREPARELAYOUT:
                // MeshPrepareLayoutArgs: Mesh (0-3), Layout (4-7). Recurse both so we collect image constants from Layout.
                if (argsOffset + 8 <= _byteCode.Length)
                {
                    uint meshAddr = (uint)(_byteCode[argsOffset] | (_byteCode[argsOffset + 1] << 8) | (_byteCode[argsOffset + 2] << 16) | (_byteCode[argsOffset + 3] << 24));
                    uint layoutAddr = (uint)(_byteCode[argsOffset + 4] | (_byteCode[argsOffset + 5] << 8) | (_byteCode[argsOffset + 6] << 16) | (_byteCode[argsOffset + 7] << 24));
                    foreach (uint a in new[] { meshAddr, layoutAddr })
                    {
                        uint o = AddressToByteCodeOffset(a);
                        if (o != 0) { var (xi, xm) = CollectConstantsFromAddress(o, paramIndexToOptionIndex, visited, constLogDepth >= 0 ? constLogDepth + 1 : -1); foreach (var i in xi) images.Add(i); foreach (var m in xm) meshes.Add(m); }
                    }
                }
                break;
            case EOpType.ME_APPLYLAYOUT:
            case EOpType.ME_DIFFERENCE:
            case EOpType.ME_MORPH:
            case EOpType.ME_FORMAT:
                if (argsOffset + 8 <= _byteCode.Length)
                {
                    uint a1 = (uint)(_byteCode[argsOffset] | (_byteCode[argsOffset + 1] << 8) | (_byteCode[argsOffset + 2] << 16) | (_byteCode[argsOffset + 3] << 24));
                    uint a2 = (uint)(_byteCode[argsOffset + 4] | (_byteCode[argsOffset + 5] << 8) | (_byteCode[argsOffset + 6] << 16) | (_byteCode[argsOffset + 7] << 24));
                    foreach (uint a in new[] { a1, a2 }) { uint o = AddressToByteCodeOffset(a); if (o != 0) { var (xi, xm) = CollectConstantsFromAddress(o, paramIndexToOptionIndex, visited, constLogDepth >= 0 ? constLogDepth + 1 : -1); foreach (var i in xi) images.Add(i); foreach (var m in xm) meshes.Add(m); } }
                }
                break;
            case EOpType.IM_PIXELFORMAT:
            case EOpType.IM_MIPMAP:
            case EOpType.IM_TRANSFORM:
                if (argsOffset + 4 <= _byteCode.Length)
                {
                    uint src = (uint)(_byteCode[argsOffset] | (_byteCode[argsOffset + 1] << 8) | (_byteCode[argsOffset + 2] << 16) | (_byteCode[argsOffset + 3] << 24));
                    uint o = AddressToByteCodeOffset(src);
                    if (o != 0) { var (xi, xm) = CollectConstantsFromAddress(o, paramIndexToOptionIndex, visited, constLogDepth >= 0 ? constLogDepth + 1 : -1); foreach (var i in xi) images.Add(i); foreach (var m in xm) meshes.Add(m); }
                }
                break;
            case EOpType.ME_SKELETALMESH_BREAK:
                if (argsOffset + 8 <= _byteCode.Length)
                {
                    uint a1 = (uint)(_byteCode[argsOffset] | (_byteCode[argsOffset + 1] << 8) | (_byteCode[argsOffset + 2] << 16) | (_byteCode[argsOffset + 3] << 24));
                    uint a2 = (uint)(_byteCode[argsOffset + 4] | (_byteCode[argsOffset + 5] << 8) | (_byteCode[argsOffset + 6] << 16) | (_byteCode[argsOffset + 7] << 24));
                    uint o1 = AddressToByteCodeOffset(a1), o2 = AddressToByteCodeOffset(a2);
                    if (doLog) MutableResolverDebugLog.Log($"CONST_ME_SKELETAL_BREAK\tdepth={constLogDepth}\ta1={a1}\to1={o1}\ta2={a2}\to2={o2}");
                    foreach (uint o in new[] { o1, o2 })
                        if (o != 0) { var (xi, xm) = CollectConstantsFromAddress(o, paramIndexToOptionIndex, visited, constLogDepth >= 0 ? constLogDepth + 1 : -1); foreach (var i in xi) images.Add(i); foreach (var m in xm) meshes.Add(m); }
                }
                break;
            case EOpType.ME_OPTIMIZESKINNING:
                if (argsOffset + 4 <= _byteCode.Length)
                {
                    uint src = (uint)(_byteCode[argsOffset] | (_byteCode[argsOffset + 1] << 8) | (_byteCode[argsOffset + 2] << 16) | (_byteCode[argsOffset + 3] << 24));
                    uint o = AddressToByteCodeOffset(src);
                    if (o != 0) { var (xi, xm) = CollectConstantsFromAddress(o, paramIndexToOptionIndex, visited, constLogDepth >= 0 ? constLogDepth + 1 : -1); foreach (var i in xi) images.Add(i); foreach (var m in xm) meshes.Add(m); }
                }
                break;
            case EOpType.IN_ADDSTRING:
                // Instance string op; no mesh/image constants to collect from this branch
                break;
            case EOpType.ME_ADDMETADATA:
                if (argsOffset + 4 <= _byteCode.Length)
                {
                    uint src = (uint)(_byteCode[argsOffset] | (_byteCode[argsOffset + 1] << 8) | (_byteCode[argsOffset + 2] << 16) | (_byteCode[argsOffset + 3] << 24));
                    uint o = AddressToByteCodeOffset(src);
                    if (o != 0) { var (xi, xm) = CollectConstantsFromAddress(o, paramIndexToOptionIndex, visited, constLogDepth >= 0 ? constLogDepth + 1 : -1); foreach (var i in xi) images.Add(i); foreach (var m in xm) meshes.Add(m); }
                }
                break;
            case EOpType.IM_REFERENCE:
                // ResourceReferenceArgs: first 4 bytes can be an image address in some layouts; recurse to keep chain.
                if (argsOffset + 4 <= _byteCode.Length)
                {
                    uint a = (uint)(_byteCode[argsOffset] | (_byteCode[argsOffset + 1] << 8) | (_byteCode[argsOffset + 2] << 16) | (_byteCode[argsOffset + 3] << 24));
                    uint o = AddressToByteCodeOffset(a);
                    if (o != 0) { var (xi, xm) = CollectConstantsFromAddress(o, paramIndexToOptionIndex, visited, constLogDepth >= 0 ? constLogDepth + 1 : -1); foreach (var i in xi) images.Add(i); foreach (var m in xm) meshes.Add(m); }
                }
                break;
            case EOpType.IM_LAYERCOLOUR:
                // Base image at first 4 bytes.
                if (argsOffset + 4 <= _byteCode.Length)
                {
                    uint a = (uint)(_byteCode[argsOffset] | (_byteCode[argsOffset + 1] << 8) | (_byteCode[argsOffset + 2] << 16) | (_byteCode[argsOffset + 3] << 24));
                    uint o = AddressToByteCodeOffset(a);
                    if (o != 0) { var (xi, xm) = CollectConstantsFromAddress(o, paramIndexToOptionIndex, visited, constLogDepth >= 0 ? constLogDepth + 1 : -1); foreach (var i in xi) images.Add(i); foreach (var m in xm) meshes.Add(m); }
                }
                break;
            default:
                // Fallback: unhandled IM_* (Unreal op 34–43) often have an image child at first 4 bytes; recurse to avoid dropping chain.
                byte opByte = (byte)opType;
                if (opByte >= 34 && opByte <= 43 && argsOffset + 4 <= _byteCode.Length)
                {
                    uint a = (uint)(_byteCode[argsOffset] | (_byteCode[argsOffset + 1] << 8) | (_byteCode[argsOffset + 2] << 16) | (_byteCode[argsOffset + 3] << 24));
                    uint o = AddressToByteCodeOffset(a);
                    if (o != 0) { var (xi, xm) = CollectConstantsFromAddress(o, paramIndexToOptionIndex, visited, constLogDepth >= 0 ? constLogDepth + 1 : -1); foreach (var i in xi) images.Add(i); foreach (var m in xm) meshes.Add(m); }
                }
                break;
        }

        return (images, meshes);
    }

    /// <summary>
    /// Walks the instance tree from the given op (by byte offset), following IN_SWITCH (with param selection), IN_CONDITIONAL, IN_ADDMESH, IN_ADDIMAGE, IN_ADDSURFACE.
    /// Returns the op indices (into OpAddress) of all mesh and image roots that are selected for the given parameters.
    /// Use AddressToByteCodeOffset on each to get byte offset, then CollectConstantsFromAddress to get constants.
    /// </summary>
    public (List<uint> meshOpIndices, List<uint> imageOpIndices) CollectMeshAndImageOpIndicesFromInstanceTree(
        uint byteCodeOffset,
        IReadOnlyDictionary<int, int> paramIndexToOptionIndex,
        HashSet<uint>? visited = null,
        int depth = 0)
    {
        var meshOpIndices = new List<uint>();
        var imageOpIndices = new List<uint>();
        visited ??= [];
        if (!visited.Add(byteCodeOffset))
        {
            if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
                MutableResolverDebugLog.LogDepth(depth, $"SKIP_ALREADY_VISITED\toffset={byteCodeOffset}");
            return (meshOpIndices, imageOpIndices);
        }

        if (byteCodeOffset >= _byteCode.Length)
        {
            if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
                MutableResolverDebugLog.LogDepth(depth, $"OUT_OF_RANGE\toffset={byteCodeOffset}\tbyteCodeLen={_byteCode.Length}");
            return (meshOpIndices, imageOpIndices);
        }
        var (opType, argsOffset) = GetInstructionAt(byteCodeOffset);

        if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
            MutableResolverDebugLog.LogDepth(depth, $"OP\toffset={byteCodeOffset}\targsOffset={argsOffset}\topType={opType}");

        switch (opType)
        {
            case EOpType.IN_SWITCH:
                var (varAddress, _, useRanges, cases) = ReadSwitchOpArgs(_byteCode, argsOffset);
                int? paramIndex = ResolveVarAddressToParameterIndex(varAddress);
                int? optionIndex = paramIndex.HasValue && paramIndexToOptionIndex.TryGetValue(paramIndex.Value, out var oi) ? oi : null;
                if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
                    MutableResolverDebugLog.LogDepth(depth, $"IN_SWITCH\tvarAddress={varAddress}\tresolvedParamIndex={paramIndex?.ToString() ?? "null"}\toptionIndex={optionIndex?.ToString() ?? "null"}\tcasesCount={cases.Count}\tuseRanges={useRanges}");
                if (optionIndex.HasValue)
                {
                    foreach (var (c, size, caseAt) in cases)
                    {
                        uint caseOff = AddressToByteCodeOffset((uint)caseAt);
                        if (caseOff == 0) continue;
                        bool match = useRanges ? (optionIndex.Value >= c && optionIndex.Value < c + size) : (c == optionIndex.Value);
                        if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath) && match)
                            MutableResolverDebugLog.LogDepth(depth, $"IN_SWITCH_MATCH\tcondition={c}\tsize={size}\tcaseAt={caseAt}\tcaseByteOff={caseOff}");
                        if (match)
                        {
                            var (mOps, iOps) = CollectMeshAndImageOpIndicesFromInstanceTree(caseOff, paramIndexToOptionIndex, visited, depth + 1);
                            meshOpIndices.AddRange(mOps);
                            imageOpIndices.AddRange(iOps);
                            break;
                        }
                    }
                }
                break;
            case EOpType.IN_CONDITIONAL:
                var (_, yes, no) = ReadConditionalArgs(_byteCode, argsOffset);
                uint yesOff = AddressToByteCodeOffset(yes), noOff = AddressToByteCodeOffset(no);
                if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
                    MutableResolverDebugLog.LogDepth(depth, $"IN_CONDITIONAL\tyes={yes}\tyesOff={yesOff}\tno={no}\tnoOff={noOff}");
                if (yesOff != 0) { var (m1, i1) = CollectMeshAndImageOpIndicesFromInstanceTree(yesOff, paramIndexToOptionIndex, visited, depth + 1); meshOpIndices.AddRange(m1); imageOpIndices.AddRange(i1); }
                if (noOff != 0) { var (m2, i2) = CollectMeshAndImageOpIndicesFromInstanceTree(noOff, paramIndexToOptionIndex, visited, depth + 1); meshOpIndices.AddRange(m2); imageOpIndices.AddRange(i2); }
                break;
            case EOpType.IN_ADDMESH:
                var (instM, valueM) = ReadInstanceAddArgs(_byteCode, argsOffset);
                if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
                    MutableResolverDebugLog.LogDepth(depth, $"IN_ADDMESH\tinstance={instM}\tvalue={valueM}");
                if (valueM != 0) { meshOpIndices.Add(valueM); if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath)) MutableResolverDebugLog.LogDepth(depth, $"COLLECT_MESH\topIndex={valueM}"); }
                if (instM != 0) { uint instOff = AddressToByteCodeOffset(instM); if (instOff != 0) { var (mx, ix) = CollectMeshAndImageOpIndicesFromInstanceTree(instOff, paramIndexToOptionIndex, visited, depth + 1); meshOpIndices.AddRange(mx); imageOpIndices.AddRange(ix); } }
                break;
            case EOpType.IN_ADDIMAGE:
                var (instI, valueI) = ReadInstanceAddArgs(_byteCode, argsOffset);
                if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
                    MutableResolverDebugLog.LogDepth(depth, $"IN_ADDIMAGE\tinstance={instI}\tvalue={valueI}");
                if (valueI != 0) { imageOpIndices.Add(valueI); if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath)) MutableResolverDebugLog.LogDepth(depth, $"COLLECT_IMAGE\topIndex={valueI}"); }
                if (instI != 0) { uint instOff = AddressToByteCodeOffset(instI); if (instOff != 0) { var (mx, ix) = CollectMeshAndImageOpIndicesFromInstanceTree(instOff, paramIndexToOptionIndex, visited, depth + 1); meshOpIndices.AddRange(mx); imageOpIndices.AddRange(ix); } }
                break;
            case EOpType.IN_ADDSURFACE:
                var (instS, valueS) = ReadInstanceAddArgs(_byteCode, argsOffset);
                if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
                {
                    var raw = new List<string>();
                    if (argsOffset + 16 <= _byteCode.Length)
                        for (int j = 0; j < 16; j++) raw.Add(_byteCode[argsOffset + j].ToString("X2"));
                    MutableResolverDebugLog.LogDepth(depth, $"IN_ADDSURFACE\tinstance={instS}\tvalue={valueS}\tinstanceOff={AddressToByteCodeOffset(instS)}\tvalueOff={AddressToByteCodeOffset(valueS)}\traw16={(raw.Count == 16 ? string.Join(" ", raw) : "n/a")}");
                }
                if (instS != 0) { uint instOff = AddressToByteCodeOffset(instS); if (instOff != 0) { var (mx, ix) = CollectMeshAndImageOpIndicesFromInstanceTree(instOff, paramIndexToOptionIndex, visited, depth + 1); meshOpIndices.AddRange(mx); imageOpIndices.AddRange(ix); } }
                if (valueS != 0) { uint valOff = AddressToByteCodeOffset(valueS); if (valOff != 0) { var (mx, ix) = CollectMeshAndImageOpIndicesFromInstanceTree(valOff, paramIndexToOptionIndex, visited, depth + 1); meshOpIndices.AddRange(mx); imageOpIndices.AddRange(ix); } }
                break;
            case EOpType.IN_ADDCOMPONENT:
                var (instC, valueC) = ReadInstanceAddArgs(_byteCode, argsOffset);
                if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
                    MutableResolverDebugLog.LogDepth(depth, $"IN_ADDCOMPONENT\tinstance={instC}\tvalue={valueC}");
                if (instC != 0) { uint instOff = AddressToByteCodeOffset(instC); if (instOff != 0) { var (mx, ix) = CollectMeshAndImageOpIndicesFromInstanceTree(instOff, paramIndexToOptionIndex, visited, depth + 1); meshOpIndices.AddRange(mx); imageOpIndices.AddRange(ix); } }
                if (valueC != 0) { uint valOff = AddressToByteCodeOffset(valueC); if (valOff != 0) { var (mx, ix) = CollectMeshAndImageOpIndicesFromInstanceTree(valOff, paramIndexToOptionIndex, visited, depth + 1); meshOpIndices.AddRange(mx); imageOpIndices.AddRange(ix); } }
                break;
            case EOpType.IN_ADDMATERIAL:
            case EOpType.IN_ADDOVERLAYMATERIAL:
            case EOpType.IN_ADDOVERRIDEMATERIAL:
                var (instMat, valueMat) = ReadInstanceAddArgs(_byteCode, argsOffset);
                if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
                    MutableResolverDebugLog.LogDepth(depth, $"{opType}\tinstance={instMat}\tvalue={valueMat}");
                if (instMat != 0) { uint instOff = AddressToByteCodeOffset(instMat); if (instOff != 0) { var (mx, ix) = CollectMeshAndImageOpIndicesFromInstanceTree(instOff, paramIndexToOptionIndex, visited, depth + 1); meshOpIndices.AddRange(mx); imageOpIndices.AddRange(ix); } }
                if (valueMat != 0) { uint valOff = AddressToByteCodeOffset(valueMat); if (valOff != 0) { var (mx, ix) = CollectMeshAndImageOpIndicesFromInstanceTree(valOff, paramIndexToOptionIndex, visited, depth + 1); meshOpIndices.AddRange(mx); imageOpIndices.AddRange(ix); } }
                break;
            case EOpType.IM_SWITCH:
            case EOpType.MI_SWITCH:
                // Follow selected case (or default) so we reach image/material roots from instance tree (e.g. material texture branches).
                var (imVarAddress, imDefAddress, imUseRanges, imCases) = ReadSwitchOpArgs(_byteCode, argsOffset);
                int? imParamIndex = ResolveVarAddressToParameterIndex(imVarAddress);
                int? imOptionIndex = imParamIndex.HasValue && paramIndexToOptionIndex.TryGetValue(imParamIndex.Value, out var imOi) ? imOi : null;
                if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
                    MutableResolverDebugLog.LogDepth(depth, $"{opType}\tvarAddress={imVarAddress}\tresolvedParamIndex={imParamIndex?.ToString() ?? "null"}\toptionIndex={imOptionIndex?.ToString() ?? "null"}\tcasesCount={imCases.Count}");
                uint branchOff = 0;
                if (imOptionIndex.HasValue)
                {
                    foreach (var (c, size, caseAt) in imCases)
                    {
                        bool match = imUseRanges ? (imOptionIndex.Value >= c && imOptionIndex.Value < c + size) : (c == imOptionIndex.Value);
                        if (match) { branchOff = AddressToByteCodeOffset((uint)caseAt); break; }
                    }
                }
                if (branchOff == 0 && imDefAddress != 0) branchOff = AddressToByteCodeOffset(imDefAddress);
                if (branchOff != 0) { var (mOps, iOps) = CollectMeshAndImageOpIndicesFromInstanceTree(branchOff, paramIndexToOptionIndex, visited, depth + 1); meshOpIndices.AddRange(mOps); imageOpIndices.AddRange(iOps); }
                break;
            case EOpType.IN_ADDLOD:
                // Args: uint8 LODCount, then LODCount x ADDRESS (4 bytes each). Recurse into each LOD.
                if (argsOffset + 1 <= _byteCode.Length)
                {
                    int lodCount = _byteCode[argsOffset];
                    int pos = (int)argsOffset + 1;
                    if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
                        MutableResolverDebugLog.LogDepth(depth, $"IN_ADDLOD\tLODCount={lodCount}\targsOffset={argsOffset}");
                    for (int i = 0; i < lodCount && pos + 4 <= _byteCode.Length; i++)
                    {
                        uint lodAddr = (uint)(_byteCode[pos] | (_byteCode[pos + 1] << 8) | (_byteCode[pos + 2] << 16) | (_byteCode[pos + 3] << 24));
                        pos += 4;
                        if (lodAddr != 0)
                        {
                            uint off = AddressToByteCodeOffset(lodAddr);
                            if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
                                MutableResolverDebugLog.LogDepth(depth, $"IN_ADDLOD_LOD\tlodIndex={i}\tlodAddr={lodAddr}\tbyteOff={off}");
                            if (off != 0)
                            {
                                var (mx, ix) = CollectMeshAndImageOpIndicesFromInstanceTree(off, paramIndexToOptionIndex, visited, depth + 1);
                                meshOpIndices.AddRange(mx);
                                imageOpIndices.AddRange(ix);
                            }
                        }
                    }
                }
                break;
            default:
                // Instance tree can point at mesh/image ops (e.g. ME_SKELETALMESH_BREAK); treat as mesh/image root.
                if (IsMeshProducingOp(opType))
                {
                    var opIdx = GetOpIndexFromByteCodeOffset(byteCodeOffset);
                    if (opIdx.HasValue) { meshOpIndices.Add(opIdx.Value); if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath)) MutableResolverDebugLog.LogDepth(depth, $"COLLECT_MESH_OP\topType={opType}\topIndex={opIdx.Value}"); }
                }
                else if (IsImageProducingOp(opType))
                {
                    var opIdx = GetOpIndexFromByteCodeOffset(byteCodeOffset);
                    if (opIdx.HasValue) { imageOpIndices.Add(opIdx.Value); if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath)) MutableResolverDebugLog.LogDepth(depth, $"COLLECT_IMAGE_OP\topType={opType}\topIndex={opIdx.Value}"); }
                }
                else if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
                    MutableResolverDebugLog.LogDepth(depth, $"UNHANDLED_OP\topType={opType}");
                break;
        }

        return (meshOpIndices, imageOpIndices);
    }

    private static bool IsMeshProducingOp(EOpType opType)
    {
        return opType == EOpType.ME_CONSTANT || opType == EOpType.ME_SKELETALMESH_BREAK
            || opType == EOpType.ME_MERGE || opType == EOpType.ME_APPLYLAYOUT || opType == EOpType.ME_FORMAT
            || opType == EOpType.ME_DIFFERENCE || opType == EOpType.ME_MORPH;
    }

    private static bool IsImageProducingOp(EOpType opType)
    {
        return opType == EOpType.IM_CONSTANT || opType == EOpType.IM_LAYER || opType == EOpType.IM_PIXELFORMAT || opType == EOpType.IM_MIPMAP;
    }

    /// <summary>
    /// Enumerates all IM_SWITCH and ME_SWITCH ops: (byteCodeOffset, opType, varAddress, defAddress, cases).
    /// </summary>
    public IEnumerable<(uint byteCodeOffset, EOpType opType, uint varAddress, uint defAddress, List<(int conditionOrStart, int size, uint caseAt)> cases)> EnumerateSwitchOps()
    {
        if (_program.OpAddress == null) yield break;
        for (int i = 0; i < _program.OpAddress.Length; i++)
        {
            uint offset = GetByteCodeOffset(_program.OpAddress[i]);
            if (offset >= _byteCode.Length) continue;
            byte op = _byteCode[offset];
            if (op != (byte)EOpType.IM_SWITCH && op != (byte)EOpType.ME_SWITCH) continue;
            uint argsOffset = offset + 1;
            var (varAddress, defAddress, _, cases) = ReadSwitchOpArgs(_byteCode, argsOffset);
            yield return (offset, (EOpType)op, varAddress, defAddress, cases);
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
        return new RomIdentification(_program, byRom, romType, _program.Roms?.Length ?? 0, this);
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
    private readonly MutableByteCode _byteCode;

    internal RomIdentification(FProgram program, Dictionary<uint, List<RomRef>> byRom, Func<uint, ERomDataType?> getRomType, int romCount, MutableByteCode byteCode)
    {
        _program = program;
        _byRom = byRom;
        _getRomType = getRomType;
        _romCount = romCount;
        _byteCode = byteCode ?? throw new ArgumentNullException(nameof(byteCode));
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
    /// Returns the constant mesh index (0..ConstantMeshes.Length-1) for this ROM, or null if not found.
    /// Use this as the grouping key so all ROMs belonging to the same logical mesh share the same index.
    /// </summary>
    public int? GetMeshConstantIndex(uint romIndex)
    {
        var id = GetMeshRomIdentity(romIndex);
        return id.HasValue ? id.Value.ConstantMeshIndex : null;
    }

    /// <summary>
    /// Identity for a mesh ROM from ByteCode/Program: MeshIDPrefix, skeleton constant index, and constant mesh index.
    /// SkeletonConstantIndex is -1 if the mesh has no skeleton; otherwise index into Program.ConstantSkeletons.
    /// Falls back to Program.ConstantMeshes/ConstantMeshContentIndices reverse lookup when ROM is not referenced by ME_CONSTANT.
    /// </summary>
    public MeshRomIdentity? GetMeshRomIdentity(uint romIndex)
    {
        if (_byRom.TryGetValue(romIndex, out var refs) && refs.Count > 0)
        {
            var r = refs[0];
            if (r.OpType == EOpType.ME_CONSTANT && _program.ConstantMeshes != null && r.ConstantIndex >= 0 && r.ConstantIndex < _program.ConstantMeshes.Length)
            {
                var range = _program.ConstantMeshes[r.ConstantIndex];
                return new MeshRomIdentity(range.MeshIDPrefix, r.SkeletonConstantIndex, r.ConstantIndex);
            }
        }
        var fallback = _byteCode.TryGetMeshConstantForRomIndex(romIndex);
        if (fallback.HasValue)
        {
            var (constantMeshIndex, range) = fallback.Value;
            return new MeshRomIdentity(range.MeshIDPrefix, -1, constantMeshIndex);
        }
        return null;
    }

    /// <summary>
    /// Identity for an image ROM from ByteCode/Program: constant image index and metadata (size, LOD count, format).
    /// Multiple ROMs can belong to the same constant image (one per MIP/LOD).
    /// Falls back to Program.ConstantImages/ConstantImageLODIndices reverse lookup when ROM is not referenced by IM_CONSTANT.
    /// </summary>
    public ImageRomIdentity? GetImageRomIdentity(uint romIndex)
    {
        if (_byRom.TryGetValue(romIndex, out var refs) && refs.Count > 0)
        {
            var r = refs[0];
            if (r.OpType == EOpType.IM_CONSTANT && _program.ConstantImages != null && r.ConstantIndex >= 0 && r.ConstantIndex < _program.ConstantImages.Length)
            {
                var range = _program.ConstantImages[r.ConstantIndex];
                uint? sourceId = GetRomSourceId(romIndex);
                return new ImageRomIdentity(r.ConstantIndex, range, sourceId);
            }
        }
        var fallback = _byteCode.TryGetImageConstantForRomIndex(romIndex);
        if (fallback.HasValue)
        {
            var (constantImageIndex, range) = fallback.Value;
            uint? sourceId = GetRomSourceId(romIndex);
            return new ImageRomIdentity(constantImageIndex, range, sourceId);
        }
        return null;
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
