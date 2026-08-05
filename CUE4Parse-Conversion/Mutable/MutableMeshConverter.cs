using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CUE4Parse_Conversion.Dto;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh.Buffers;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Meshes;
using CUE4Parse.UE4.Objects.RenderCore;

namespace CUE4Parse_Conversion.Mutable;

public static class MutableMeshConverter
{
    public static bool TryConvert(
        this FMesh originalMesh,
        UCustomizableObject co,
        string materialSlotName,
        [MaybeNullWhen(false)] out StaticMeshDto convertedMesh,
        List<FMesh>? additionalLods = null)
    {
        convertedMesh = null;

        if (!co.Private.TryLoad(out UCustomizableObjectPrivate coPrivate)
            || !coPrivate.ModelResources.TryLoad(out UModelResources modelResources))
            return false;

        var lodMeshes = new List<FMesh> { originalMesh };
        if (additionalLods != null)
            lodMeshes.AddRange(additionalLods);

        var lodData = new List<(uint[] Indices, MeshVertex[] Vertices, FMeshUVFloat[][] ExtraUvs, FColor[]? Colors, int NumFaces)>();
        var bounds = new FBox();
        var hasBounds = false;

        foreach (var lodMesh in lodMeshes)
        {
            if (!TryBuildLodData(lodMesh, out var indices, out var vertices, out var extraUvs, out var colors, out var numFaces))
                continue;

            lodData.Add((indices, vertices, extraUvs, colors, numFaces));
            foreach (var vertex in vertices)
            {
                if (!hasBounds)
                {
                    bounds = new FBox(vertex.Position, vertex.Position);
                    hasBounds = true;
                }
                else
                {
                    bounds += vertex.Position;
                }
            }
        }

        if (lodData.Count == 0)
            return false;

        var materials = new[] { new MeshMaterialDto(materialSlotName) };
        convertedMesh = new StaticMeshDto(co, materials, hasBounds ? bounds : new FBox());

        for (var i = 0; i < lodData.Count; i++)
        {
            var (indices, vertices, extraUvs, colors, numFaces) = lodData[i];
            var sections = new[] { new MeshSectionDto(0, 0, numFaces, true) };
            convertedMesh.LODs.Add(MeshLodDto<MeshVertex>.FromMutable(convertedMesh, (uint)i, indices, vertices, sections, extraUvs, colors));
        }

        convertedMesh.FinalizeLods();
        return true;
    }

    private static bool TryBuildLodData(
        FMesh originalMesh,
        out uint[] indices,
        out MeshVertex[] vertices,
        out FMeshUVFloat[][] extraUvs,
        out FColor[]? colors,
        out int numFaces)
    {
        indices = [];
        vertices = [];
        extraUvs = [];
        colors = null;
        numFaces = 0;

        GetBuffer(EMeshBufferSemantic.VertexIndex, originalMesh, out var indexChannel, out var indexBuffer);
        GetBuffer(EMeshBufferSemantic.Position, originalMesh, out var vertexChannel, out var vertexBuffer);
        GetBuffer(EMeshBufferSemantic.Normal, originalMesh, out var normalChannel, out var normalBuffer);
        GetBuffer(EMeshBufferSemantic.Tangent, originalMesh, out var tangentChannel, out var tangentBuffer);
        GetBuffer(EMeshBufferSemantic.TexCoords, originalMesh, out var uvChannel, out var uvBuffer);
        GetBuffer(EMeshBufferSemantic.TexCoords, originalMesh, out var uv1Channel, out var uv1Buffer, 1);
        GetBuffer(EMeshBufferSemantic.TexCoords, originalMesh, out var uv2Channel, out var uv2Buffer, 2);
        GetBuffer(EMeshBufferSemantic.Color, originalMesh, out var colorChannel, out var colorBuffer);

        if (vertexBuffer == null || normalBuffer == null || uvBuffer == null)
            return false;

        var dataConverter = new MutableDataConverter(originalMesh.IndexBuffers.ElementCount);
        indices = dataConverter.GetIndices(indexChannel, indexBuffer);
        if (indices.Length == 0)
            return false;

        numFaces = (int)(originalMesh.IndexBuffers.ElementCount / 3);
        var vertexCount = (int)originalMesh.VertexBuffers.ElementCount;
        vertices = new MeshVertex[vertexCount];

        var numExtraUvs = uv1Buffer == null ? 0 : uv2Buffer == null ? 1 : 2;
        extraUvs = new FMeshUVFloat[numExtraUvs][];
        for (var i = 0; i < numExtraUvs; i++)
            extraUvs[i] = new FMeshUVFloat[vertexCount];

        if (colorBuffer != null)
            colors = new FColor[vertexCount];

        var zeroTangent = new FPackedNormal(FVector4.ZeroVector);

        for (var i = 0; i < vertexCount; i++)
        {
            var position = dataConverter.GetVertices(vertexChannel, vertexBuffer, i);
            var normal = dataConverter.GetNormals(normalChannel, normalBuffer, i);
            var tangent = tangentBuffer != null
                ? dataConverter.GetTangent(tangentChannel, tangentBuffer, i)
                : zeroTangent;
            var uv = dataConverter.GetUVs(uvChannel, uvBuffer, i);

            vertices[i] = new MeshVertex(position, normal, tangent, uv);

            if (uv1Buffer != null)
            {
                extraUvs[0][i] = dataConverter.GetUVs(uv1Channel, uv1Buffer, i);
                if (uv2Buffer != null)
                    extraUvs[1][i] = dataConverter.GetUVs(uv2Channel, uv2Buffer, i);
            }

            if (colors != null)
                colors[i] = dataConverter.GetColor(colorChannel, colorBuffer, i);
        }

        return true;
    }

    private static void GetBuffer(
        EMeshBufferSemantic bufferType,
        FMesh mesh,
        out FMeshBufferChannel channel,
        out FMeshBuffer? vertexBuffer,
        int semanticIndex = 0)
    {
        foreach (var buffer in mesh.IndexBuffers.Buffers)
        {
            channel = buffer.Channels.FirstOrDefault(c => c.Semantic == bufferType && c.SemanticIndex == semanticIndex);
            if (channel.ComponentCount == 0) continue;

            vertexBuffer = buffer;
            return;
        }

        foreach (var buffer in mesh.VertexBuffers.Buffers)
        {
            channel = buffer.Channels.FirstOrDefault(c => c.Semantic == bufferType && c.SemanticIndex == semanticIndex);
            if (channel.ComponentCount == 0) continue;

            vertexBuffer = buffer;
            return;
        }

        channel = new FMeshBufferChannel();
        vertexBuffer = null;
    }
}
