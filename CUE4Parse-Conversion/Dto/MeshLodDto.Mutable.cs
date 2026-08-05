using System;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Meshes;

namespace CUE4Parse_Conversion.Dto;

public partial class MeshLodDto<TVertex>
{
    internal static MeshLodDto<MeshVertex> FromMutable(
        StaticMeshDto owner,
        uint sourceLodIndex,
        uint[] indices,
        MeshVertex[] vertices,
        MeshSectionDto[] sections,
        FMeshUVFloat[][] extraUvs,
        FColor[]? vertexColors = null)
    {
        return new MeshLodDto<MeshVertex>(owner, sourceLodIndex, indices, vertices, sections, extraUvs, vertexColors);
    }
}
