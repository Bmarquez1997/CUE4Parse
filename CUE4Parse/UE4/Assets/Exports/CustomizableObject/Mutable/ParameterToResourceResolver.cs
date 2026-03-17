using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Parameters;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;

/// <summary>
/// Resolves CustomizableObjectInstance parameter values (e.g. "Body Textures" = "CrispSeason") to the
/// mesh and image resources (constant indices and ROM indices) that the ByteCode selects for those parameters.
/// Based on Unreal Mutable: parameters feed NU_PARAMETER; IM_SWITCH/ME_SWITCH compare that int to case Conditions
/// (option indices); the matching case branch is the root of the selected image/mesh subtree.
/// Use with instance: load parent CustomizableObject.Model.Program, then pass instance descriptor IntParameters
/// as (ParameterName, ParameterValueName) to ResolveFromInstanceIntParameters to get the meshes and textures
/// that those two properties reference.
/// </summary>
public class ParameterToResourceResolver
{
    private readonly FProgram _program;
    private readonly MutableByteCode _byteCode;
    private readonly Dictionary<string, int> _paramNameToIndex;

    public ParameterToResourceResolver(FProgram program)
    {
        _program = program ?? throw new ArgumentNullException(nameof(program));
        _byteCode = new MutableByteCode(program);
        _paramNameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (program.Parameters != null)
        {
            for (int i = 0; i < program.Parameters.Length; i++)
            {
                var name = program.Parameters[i].Name;
                if (!string.IsNullOrEmpty(name))
                    _paramNameToIndex[name] = i;
            }
        }
    }

    /// <summary>
    /// Converts instance int parameter values (ParameterName -> ParameterValueName) to a map
    /// parameter index -> option index (0-based) for use in ByteCode SWITCH resolution.
    /// ParameterValueName must match Program.Parameters[paramIndex].PossibleValues[optionIndex].Name.
    /// </summary>
    public static Dictionary<int, int> InstanceParamsToOptionIndices(
        FProgram program,
        IReadOnlyCollection<(string ParameterName, string ParameterValueName)> intParams)
    {
        var result = new Dictionary<int, int>();
        if (program.Parameters == null || intParams == null) return result;
        foreach (var (paramName, valueName) in intParams)
        {
            if (string.IsNullOrEmpty(paramName)) continue;
            int paramIndex = -1;
            for (int i = 0; i < program.Parameters.Length; i++)
            {
                if (string.Equals(program.Parameters[i].Name, paramName, StringComparison.OrdinalIgnoreCase))
                {
                    paramIndex = i;
                    break;
                }
            }
            if (paramIndex < 0) continue;
            var possible = program.Parameters[paramIndex].PossibleValues;
            if (possible == null) continue;
            for (int v = 0; v < possible.Length; v++)
            {
                if (string.Equals(possible[v].Name, valueName, StringComparison.OrdinalIgnoreCase))
                {
                    result[paramIndex] = v;
                    break;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Gets parameter index by name (case-insensitive). Returns null if not found.
    /// </summary>
    public int? GetParameterIndex(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName)) return null;
        return _paramNameToIndex.TryGetValue(parameterName, out var i) ? i : null;
    }

    /// <summary>
    /// Resolves the given instance parameter set to the set of image and mesh constant indices
    /// (and optionally ROM indices) that the ByteCode selects when those parameters are active.
    /// </summary>
    /// <param name="paramIndexToOptionIndex">Parameter index -> option index (from InstanceParamsToOptionIndices or GetParameterIndex + PossibleValues index).</param>
    /// <param name="includeRomIndices">If true, also resolve constant indices to ROM indices via Program tables.</param>
    public (IReadOnlyCollection<int> imageConstantIndices, IReadOnlyCollection<int> meshConstantIndices, IReadOnlyCollection<uint>? imageRomIndices, IReadOnlyCollection<uint>? meshRomIndices) ResolveToResources(
        IReadOnlyDictionary<int, int> paramIndexToOptionIndex,
        bool includeRomIndices = true)
    {
        var imageConstants = new HashSet<int>();
        var meshConstants = new HashSet<int>();
        var visited = new HashSet<uint>();
        // Use separate visited set for constant collection so mesh/image root offsets aren't already marked from instance tree walk
        var visitedConstants = new HashSet<uint>();

        if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
        {
            MutableResolverDebugLog.Log("=== ResolveToResources ===");
            MutableResolverDebugLog.Log($"PROGRAM\tStatesLength={_program.States?.Length ?? 0}\tOpAddressLength={_program.OpAddress?.Length ?? 0}\tParametersCount={_program.Parameters?.Length ?? 0}\tByteCodeLength={_program.ByteCode?.Length ?? 0}");
            if (_program.Parameters != null)
                for (int i = 0; i < _program.Parameters.Length; i++)
                {
                    var param = _program.Parameters[i];
                    MutableResolverDebugLog.Log(
                        $"PARAM_DESC\tindex={i}\tname={param.Name}\tvaluesCount={param.PossibleValues?.Length ?? 0}");
                    if (param.Name.Contains("Color") || param.PossibleValues?.Length == 0) continue;
                    MutableResolverDebugLog.Log(string.Join("\n", param.PossibleValues.Select(p => $"\t{p.Value:D3} = {p.Name}")));
                }

            MutableResolverDebugLog.Log($"PARAM_MAP\tcount={paramIndexToOptionIndex.Count}");
            foreach (var kv in paramIndexToOptionIndex)
                MutableResolverDebugLog.Log($"PARAM_MAP_ENTRY\tparamIndex={kv.Key}\toptionIndex={kv.Value}");
            if (_program.OpAddress != null)
            {
                MutableResolverDebugLog.Log($"OP_ADDRESS_SAMPLE\tfirst 30 opIndex->byteOffset->opType:");
                for (int i = 0; i < Math.Min(30, _program.OpAddress.Length); i++)
                {
                    uint bo = _byteCode.AddressToByteCodeOffset((uint)i);
                    var ot = bo < (_program.ByteCode?.Length ?? 0) ? _byteCode.GetOpTypeAt(bo) : EOpType.NONE;
                    MutableResolverDebugLog.Log($"OP_ADDRESS\topIndex={i}\tbyteOffset={bo}\topType={ot}");
                }
            }
        }

        // Collect image constants from IM_SWITCH before mesh roots so shared bytecode nodes aren't marked visited and skipped.
        var allSwitches = _byteCode.EnumerateSwitchOps().ToList();
        int imSwitchDefaultPassCount = 0;
        foreach (var (_, opType, varAddress, defAddress, cases) in allSwitches)
        {
            if (opType != EOpType.IM_SWITCH) continue;
            int? paramIndex = _byteCode.ResolveVarAddressToParameterIndex(varAddress);
            int optionIndex = -1;
            bool haveOption = paramIndex.HasValue && paramIndexToOptionIndex.TryGetValue(paramIndex.Value, out optionIndex);
            // Collect from matched case when we have an option; also collect from default when no case matched.
            bool collectedFromCase = false;
            if (haveOption)
            {
                foreach (var (c, size, caseAt) in cases)
                {
                    bool useRanges = size != 0;
                    if (useRanges ? optionIndex >= c && optionIndex < c + size : c == optionIndex)
                    {
                        uint caseOff = _byteCode.AddressToByteCodeOffset((uint)caseAt);
                        if (caseOff != 0)
                        {
                            var (imgC, meshC) = _byteCode.CollectConstantsFromAddress(caseOff, paramIndexToOptionIndex, visitedConstants, constLogDepth: -1);
                            foreach (var i in imgC) imageConstants.Add(i);
                            foreach (var m in meshC) meshConstants.Add(m);
                            collectedFromCase = true;
                        }
                        break;
                    }
                }
            }
            if (!collectedFromCase && defAddress != 0)
            {
                uint defOff = _byteCode.AddressToByteCodeOffset(defAddress);
                if (defOff != 0)
                {
                    imSwitchDefaultPassCount++;
                    int constLog = (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath) && imSwitchDefaultPassCount == 1) ? 0 : -1;
                    var (imgC, meshC) = _byteCode.CollectConstantsFromAddress(defOff, paramIndexToOptionIndex, visitedConstants, constLog);
                    foreach (var i in imgC) imageConstants.Add(i);
                    foreach (var m in meshC) meshConstants.Add(m);
                }
            }
        }

        // Start from each state root (instance tree): follow IN_SWITCH/IN_CONDITIONAL/IN_ADDMESH/IN_ADDIMAGE/IN_ADDSURFACE/IN_ADDCOMPONENT to get mesh/image op indices, then collect constants from each.
        if (_program.States != null)
        {
            for (int s = 0; s < _program.States.Length; s++)
            {
                uint rootOp = _program.States[s].Root;
                if (rootOp == 0) continue;
                uint rootByteOff = _byteCode.AddressToByteCodeOffset(rootOp);
                if (rootByteOff == 0) continue;
                var rootOpType = _byteCode.GetOpTypeAt(rootByteOff);
                if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
                    MutableResolverDebugLog.Log($"STATE_ROOT\tstateIndex={s}\trootOp={rootOp}\trootByteOff={rootByteOff}\trootOpType={rootOpType}");
                var (meshOpIndices, imageOpIndices) = _byteCode.CollectMeshAndImageOpIndicesFromInstanceTree(rootByteOff, paramIndexToOptionIndex, visited);
                if (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath))
                    MutableResolverDebugLog.Log($"STATE_RESULT\tstateIndex={s}\tmeshOpIndicesCount={meshOpIndices.Count}\timageOpIndicesCount={imageOpIndices.Count}");
                int meshRootIndex = 0;
                foreach (uint meshOp in meshOpIndices)
                {
                    uint off = _byteCode.AddressToByteCodeOffset(meshOp);
                    if (off == 0) continue;
                    // Log CONST_* only for first mesh root to keep log manageable
                    int constLogDepth = (meshRootIndex == 0 && !string.IsNullOrEmpty(MutableResolverDebugLog.LogPath)) ? 0 : -1;
                    var (_, meshC) = _byteCode.CollectConstantsFromAddress(off, paramIndexToOptionIndex, visitedConstants, constLogDepth);
                    foreach (var m in meshC) meshConstants.Add(m);
                    meshRootIndex++;
                }
                foreach (uint imageOp in imageOpIndices)
                {
                    uint off = _byteCode.AddressToByteCodeOffset(imageOp);
                    if (off == 0) continue;
                    var (imgC, _) = _byteCode.CollectConstantsFromAddress(off, paramIndexToOptionIndex, visitedConstants, constLogDepth: 0);
                    foreach (var i in imgC) imageConstants.Add(i);
                }
            }
        }

        int imSwitchDefaultCount = 0;
        foreach (var (_, opType, varAddress, defAddress, cases) in allSwitches)
        {
            if (opType == EOpType.IM_SWITCH) continue; // Already processed above before mesh roots.
            bool matched = false;
            int? paramIndex = _byteCode.ResolveVarAddressToParameterIndex(varAddress);
            if (paramIndex.HasValue && paramIndexToOptionIndex.TryGetValue(paramIndex.Value, out int optionIndex))
            {
                foreach (var (c, size, caseAt) in cases)
                {
                    uint caseOff = _byteCode.AddressToByteCodeOffset((uint)caseAt);
                    if (caseOff == 0) continue;
                    bool useRanges = size != 0;
                    bool match = useRanges ? (optionIndex >= c && optionIndex < c + size) : (c == optionIndex);
                    if (!match) continue;
                    var (imgC, meshC) = _byteCode.CollectConstantsFromAddress(caseOff, paramIndexToOptionIndex, visitedConstants, constLogDepth: 0);
                    foreach (var i in imgC) imageConstants.Add(i);
                    foreach (var m in meshC) meshConstants.Add(m);
                    matched = true;
                    break;
                }
            }
            // When no case matched (or param not in map), collect from default branch to get image/mesh constants
            if (!matched && defAddress != 0)
            {
                uint defOff = _byteCode.AddressToByteCodeOffset(defAddress);
                if (defOff != 0)
                {
                    bool isImageSwitch = opType == EOpType.IM_SWITCH;
                    if (isImageSwitch) imSwitchDefaultCount++;
                    int constLog = (!string.IsNullOrEmpty(MutableResolverDebugLog.LogPath) && isImageSwitch && imSwitchDefaultCount == 1) ? 0 : -1;
                    var (imgC, meshC) = _byteCode.CollectConstantsFromAddress(defOff, paramIndexToOptionIndex, visitedConstants, constLog);
                    foreach (var i in imgC) imageConstants.Add(i);
                    foreach (var m in meshC) meshConstants.Add(m);
                }
            }
        }

        // If no image constants were found from instance/switches, include all IM_CONSTANT indices from the program so the COI has image references.
        if (imageConstants.Count == 0)
        {
            foreach (var (_, opType, constantIndex, _) in _byteCode.EnumerateConstantReferences())
            {
                if (opType == EOpType.IM_CONSTANT && constantIndex >= 0)
                    imageConstants.Add(constantIndex);
            }
        }

        IReadOnlyCollection<uint>? imageRoms = null;
        IReadOnlyCollection<uint>? meshRoms = null;
        if (includeRomIndices)
        {
            var ir = new HashSet<uint>();
            foreach (int ic in imageConstants)
            {
                foreach (var rom in _byteCode.GetImageConstantRomIndices(ic))
                    ir.Add(rom);
            }
            imageRoms = ir;
            var mr = new HashSet<uint>();
            foreach (int mc in meshConstants)
            {
                foreach (var rom in _byteCode.GetMeshConstantRomIndices(mc))
                    mr.Add(rom);
            }
            meshRoms = mr;
        }

        return (imageConstants.ToList(), meshConstants.ToList(), imageRoms, meshRoms);
    }

    /// <summary>
    /// Resolves instance int parameters (e.g. from FCustomizableObjectInstanceDescriptor.IntParameters)
    /// to the mesh and image resources selected by those parameters.
    /// </summary>
    public (IReadOnlyCollection<int> imageConstantIndices, IReadOnlyCollection<int> meshConstantIndices, IReadOnlyCollection<uint>? imageRomIndices, IReadOnlyCollection<uint>? meshRomIndices) ResolveFromInstanceIntParameters(
        IEnumerable<(string ParameterName, string ParameterValueName)> intParameters,
        bool includeRomIndices = true)
    {
        var list = intParameters?.ToList() ?? [];
        var paramToOption = InstanceParamsToOptionIndices(_program, list);
        return ResolveToResources(paramToOption, includeRomIndices);
    }
}
