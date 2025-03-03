using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Meshes;
using CUE4Parse.UE4.Objects.RenderCore;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Skeleton;
using CUE4Parse.UE4.Assets.Objects;
using Serilog;

namespace CUE4Parse_Conversion.Meshes;

public static class MeshConverter
{
    public static bool TryConvert(this FMesh originalMesh, UCustomizableObject co, string materialSlotName, out CSkeletalMesh convertedMesh, List<FMesh>? additionalLods = null)
     {
         convertedMesh = new CSkeletalMesh();
         
         if (!co.Private.TryLoad(out var ueExport) || ueExport is not UCustomizableObjectPrivate coPrivate)
             return false;

         USkeleton? skeleton = null;
         var skeletonPath = coPrivate.ModelResources.Skeletons[originalMesh.SkeletonIDs.LastOrDefault(0u)];
         if (skeletonPath.TryLoad(out var skeletonExport)
             && skeletonExport is USkeleton skeletonCast
             && skeletonCast.TryConvert(out var bones, out var box))
         {
             skeleton = skeletonCast;
             convertedMesh.RefSkeleton.AddRange(bones);
         }

         var lod0 = BuildLodObject(originalMesh, coPrivate, skeleton, materialSlotName);
         if (lod0 == null) return false;
         
         convertedMesh.LODs.Add(lod0);
         if (additionalLods != null)
         {
             foreach (var lod in additionalLods)
             {
                 GetBuffer(EMeshBufferSemantic.Position, lod, out var vertChannel, out var vertBuffer);
                 if (vertBuffer == null) continue;
                 
                 var lodMesh = BuildLodObject(lod, coPrivate, skeleton, materialSlotName);
                 if (lodMesh == null) continue;
                 
                 convertedMesh.LODs.Add(lodMesh);
             }
         }
             

         convertedMesh.FinalizeMesh();
         return true;
     }

    // public static CSkelMeshLod TryConvert(this FMesh originalMesh, UCustomizableObject co, out CSkeletalMesh convertedMesh)
    private static CSkelMeshLod? BuildLodObject(FMesh originalMesh, UCustomizableObjectPrivate coPrivate, USkeleton? skeleton, string materialSlotName)
    {
        // convertedMesh = new CSkeletalMesh();
        
        GetBuffer(EMeshBufferSemantic.VertexIndex, originalMesh, out var indexChannel, out var indexBuffer);
        GetBuffer(EMeshBufferSemantic.Position, originalMesh, out var vertexChannel, out var vertexBuffer);
        GetBuffer(EMeshBufferSemantic.Normal, originalMesh, out var normalChannel, out var normalBuffer);
        GetBuffer(EMeshBufferSemantic.Tangent, originalMesh, out var tangentChannel, out var tangentBuffer);
        GetBuffer(EMeshBufferSemantic.TexCoords, originalMesh, out var uvChannel, out var uvBuffer);
        GetBuffer(EMeshBufferSemantic.TexCoords, originalMesh, out var uv1Channel, out var uv1Buffer, 1);
        GetBuffer(EMeshBufferSemantic.TexCoords, originalMesh, out var uv2Channel, out var uv2Buffer, 2);
        GetBuffer(EMeshBufferSemantic.Color, originalMesh, out var colorChannel, out var colorBuffer);
        GetBuffer(EMeshBufferSemantic.BoneIndices, originalMesh, out var boneIndexChannel, out var boneIndexBuffer);
        GetBuffer(EMeshBufferSemantic.BoneWeights, originalMesh, out var weightChannel, out var weightBuffer);

        var dataConverter = new MutableDataConverter(originalMesh.IndexBuffers.ElementCount);
        var indices = dataConverter.GetIndices(indexChannel, indexBuffer);

        var mutSkelMeshLod = new CSkelMeshLod
        {
            NumTexCoords = uv1Buffer == null ? 1 : uv2Buffer == null ? 2 : 3,
            HasNormals = true,
            HasTangents = tangentBuffer != null,
            Indices = new Lazy<FRawStaticIndexBuffer>(() => new FRawStaticIndexBuffer
            {
                Indices32 = indices
            }),
            Sections = new Lazy<CMeshSection[]>(() =>
            {
                var sections = new CMeshSection[1];
                sections[0] = new CMeshSection(0, 0, Convert.ToInt32(originalMesh.IndexBuffers.ElementCount / 3), materialSlotName, null);
                return sections;
            })
        };

        var vertexCount = originalMesh.VertexBuffers.ElementCount;
        mutSkelMeshLod.AllocateVerts((int) vertexCount);
        
        var boneMap = BuildBoneIndexMap(originalMesh.BoneMap, coPrivate.ModelResources.BoneNamesMap, skeleton.ReferenceSkeleton.FinalNameToIndexMap);
        
        if (colorBuffer != null)
            mutSkelMeshLod.AllocateVertexColorBuffer();

        for (var i = 0; i < vertexCount; i++)
        {
            mutSkelMeshLod.Verts[i].Position = dataConverter.GetVertices(vertexChannel, vertexBuffer, i);
            mutSkelMeshLod.Verts[i].Normal = dataConverter.GetNormals(normalChannel, normalBuffer, i);
            if (tangentBuffer != null) mutSkelMeshLod.Verts[i].Tangent = dataConverter.GetTangent(tangentChannel, tangentBuffer, i);
            mutSkelMeshLod.Verts[i].UV = dataConverter.GetUVs(uvChannel, uvBuffer, i);

            if (uv1Buffer != null)
            {
                mutSkelMeshLod.ExtraUV.Value[0][i] = dataConverter.GetUVs(uv1Channel, uv1Buffer, i);
                if (uv2Buffer != null)
                    mutSkelMeshLod.ExtraUV.Value[1][i] = dataConverter.GetUVs(uv2Channel, uv2Buffer, i);
            }

            if (mutSkelMeshLod.VertexColors != null)
                mutSkelMeshLod.VertexColors[i] = dataConverter.GetColor(colorChannel, colorBuffer, i);
            
            if (boneIndexChannel == null || weightChannel == null || boneIndexBuffer == null || weightBuffer == null || boneMap == null) continue;
            foreach (var (boneId, weight) in dataConverter.GetWeights(boneIndexChannel, weightChannel, boneIndexBuffer, weightBuffer, i))
            {
                if (weight == 0) continue;
                var boneIndex = boneMap[boneId];
                mutSkelMeshLod.Verts[i].AddInfluence(boneIndex, weight);
            }
        }

        return mutSkelMeshLod;
    }
    
     private static Dictionary<short, short>? BuildBoneIndexMap(FBoneName[] meshBoneMap,
         UScriptMap? boneNameMap, Dictionary<string, int> finalNameToIndexMap)
     {
         if (boneNameMap == null) return null;
         Dictionary<short, short> boneIndexMap = [];
         for (var i = 0; i < meshBoneMap.Length; i++)
         {
             var boneID = meshBoneMap[i].Id;
             var boneName = boneNameMap.Properties.First(mapEntry => mapEntry.Value.GetValue<uint>() == boneID).Key
                 .GetValue<string>();
             if (boneName == null) continue;
             var boneIndex = finalNameToIndexMap.First(indexMapEntry =>
                 indexMapEntry.Key.Equals(boneName, StringComparison.OrdinalIgnoreCase)).Value;
             boneIndexMap[(short)i] = (short)boneIndex;
         }

         return boneIndexMap;
     }

    public static bool TryConvert(this USkeletalMesh originalMesh, out CSkeletalMesh convertedMesh)
    {
        convertedMesh = new CSkeletalMesh();
        if (originalMesh.LODModels == null) return false;

        convertedMesh.BoundingSphere = new FSphere(0f, 0f, 0f, originalMesh.ImportedBounds.SphereRadius / 2);
        convertedMesh.BoundingBox = new FBox(
            originalMesh.ImportedBounds.Origin - originalMesh.ImportedBounds.BoxExtent,
            originalMesh.ImportedBounds.Origin + originalMesh.ImportedBounds.BoxExtent);

        foreach (var srcLod in originalMesh.LODModels)
        {
            if (srcLod.SkipLod) continue;

            var numTexCoords = srcLod.NumTexCoords;
            if (numTexCoords > Constants.MAX_MESH_UV_SETS)
                throw new ParserException($"Skeletal mesh has too many UV sets ({numTexCoords})");

            var skeletalMeshLod = new CSkelMeshLod
            {
                NumTexCoords = numTexCoords,
                HasNormals = true,
                HasTangents = true,
                Indices = new Lazy<FRawStaticIndexBuffer>(() => new FRawStaticIndexBuffer
                {
                    Indices16 = srcLod.Indices.Indices16, Indices32 = srcLod.Indices.Indices32
                }),
                Sections = new Lazy<CMeshSection[]>(() =>
                {
                    var sections = new CMeshSection[srcLod.Sections.Length];
                    for (var j = 0; j < sections.Length; j++)
                    {
                        int materialIndex = srcLod.Sections[j].MaterialIndex;
                        if (materialIndex < 0) // UE4 using Clamp(0, Materials.Num()), not Materials.Num()-1
                        {
                            materialIndex = 0;
                        }
                        else while (materialIndex >= originalMesh.Materials?.Length)
                        {
                            materialIndex--;
                        }

                        if (materialIndex < 0) sections[j] = new CMeshSection(srcLod.Sections[j]);
                        else
                        {
                            sections[j] = new CMeshSection(materialIndex, srcLod.Sections[j],
                                originalMesh.SkeletalMaterials[materialIndex].MaterialSlotName.Text,
                                originalMesh.SkeletalMaterials[materialIndex].Material);
                        }
                    }

                    return sections;
                })
            };

            var bUseVerticesFromSections = false;
            var vertexCount = srcLod.VertexBufferGPUSkin.GetVertexCount();
            if (vertexCount == 0 && srcLod.Sections.Length > 0 && srcLod.Sections[0].SoftVertices.Length > 0)
            {
                bUseVerticesFromSections = true;
                foreach (var section in srcLod.Sections)
                {
                    vertexCount += section.SoftVertices.Length;
                }
            }

            skeletalMeshLod.AllocateVerts(vertexCount);

            var chunkIndex = -1;
            var chunkVertexIndex = 0;
            long lastChunkVertex = -1;
            ushort[]? boneMap = null;
            var vertBuffer = srcLod.VertexBufferGPUSkin;

            if (srcLod.ColorVertexBuffer.Data.Length == vertexCount)
                skeletalMeshLod.AllocateVertexColorBuffer();

            for (var vert = 0; vert < vertexCount; vert++)
            {
                while (vert >= lastChunkVertex) // this will fix any issues with empty chunks or sections
                {
                    if (srcLod.Chunks.Length > 0) // proceed to next chunk or section
                    {
                        // pre-UE4.13 code: chunks
                        var c = srcLod.Chunks[++chunkIndex];
                        lastChunkVertex = c.BaseVertexIndex + c.NumRigidVertices + c.NumSoftVertices;
                        boneMap = c.BoneMap;
                    }
                    else
                    {
                        // UE4.13+ code: chunk information migrated to sections
                        var s = srcLod.Sections[++chunkIndex];
                        lastChunkVertex = s.BaseVertexIndex + s.NumVertices;
                        boneMap = s.BoneMap;
                    }

                    chunkVertexIndex = 0;
                }

                FSkelMeshVertexBase v; // has everything but UV[]
                if (bUseVerticesFromSections)
                {
                    var v0 = srcLod.Sections[chunkIndex].SoftVertices[chunkVertexIndex++];
                    v = v0;

                    skeletalMeshLod.Verts[vert].UV = v0.UV[0]; // UV: simply copy float data
                    for (var texCoordIndex = 1; texCoordIndex < numTexCoords; texCoordIndex++)
                    {
                        skeletalMeshLod.ExtraUV.Value[texCoordIndex - 1][vert] = v0.UV[texCoordIndex];
                    }
                }
                else if (!vertBuffer.bUseFullPrecisionUVs)
                {
                    var v0 = vertBuffer.VertsHalf[vert];
                    v = v0;

                    skeletalMeshLod.Verts[vert].UV = (FMeshUVFloat) v0.UV[0]; // UV: convert half -> float
                    for (var texCoordIndex = 1; texCoordIndex < numTexCoords; texCoordIndex++)
                    {
                        skeletalMeshLod.ExtraUV.Value[texCoordIndex - 1][vert] = (FMeshUVFloat) v0.UV[texCoordIndex];
                    }
                }
                else
                {
                    var v0 = vertBuffer.VertsFloat[vert];
                    v = v0;

                    skeletalMeshLod.Verts[vert].UV = v0.UV[0]; // UV: simply copy float data
                    for (var texCoordIndex = 1; texCoordIndex < numTexCoords; texCoordIndex++)
                    {
                        skeletalMeshLod.ExtraUV.Value[texCoordIndex - 1][vert] = v0.UV[texCoordIndex];
                    }
                }

                skeletalMeshLod.Verts[vert].Position = v.Pos;
                UnpackNormals(v.Normal, skeletalMeshLod.Verts[vert]);
                if (skeletalMeshLod.VertexColors != null)
                {
                    skeletalMeshLod.VertexColors[vert] = srcLod.ColorVertexBuffer.Data[vert];
                }

                foreach (var (weight, boneIndex) in v.Infs.BoneWeight.Zip(v.Infs.BoneIndex))
                {
                    if (weight != 0)
                    {
                        var bone = (short)boneMap[boneIndex];
                        skeletalMeshLod.Verts[vert].AddInfluence(bone, weight);
                    }
                }
            }

            convertedMesh.LODs.Add(skeletalMeshLod);
        }

        for (var i = 0; i < originalMesh.ReferenceSkeleton.FinalRefBoneInfo.Length; i++)
        {
            var skeletalMeshBone = new CSkelMeshBone
            {
                Name = originalMesh.ReferenceSkeleton.FinalRefBoneInfo[i].Name,
                ParentIndex = originalMesh.ReferenceSkeleton.FinalRefBoneInfo[i].ParentIndex,
                Position = originalMesh.ReferenceSkeleton.FinalRefBonePose[i].Translation,
                Orientation = originalMesh.ReferenceSkeleton.FinalRefBonePose[i].Rotation
            };

            // if (i >= 1) // fix skeleton; all bones but 0
            //     skeletalMeshBone.Orientation.Conjugate();

            convertedMesh.RefSkeleton.Add(skeletalMeshBone);
        }

        convertedMesh.FinalizeMesh();
        return true;
    }

    public static bool TryConvert(this USkeleton originalSkeleton, out List<CSkelMeshBone> bones, out FBox box)
    {
        bones = new List<CSkelMeshBone>();
        box = new FBox();
        for (var i = 0; i < originalSkeleton.ReferenceSkeleton.FinalRefBoneInfo.Length; i++)
        {
            var skeletalMeshBone = new CSkelMeshBone
            {
                Name = originalSkeleton.ReferenceSkeleton.FinalRefBoneInfo[i].Name,
                ParentIndex = originalSkeleton.ReferenceSkeleton.FinalRefBoneInfo[i].ParentIndex,
                Position = originalSkeleton.ReferenceSkeleton.FinalRefBonePose[i].Translation,
                Orientation = originalSkeleton.ReferenceSkeleton.FinalRefBonePose[i].Rotation,
            };

            // if (i >= 1) // fix skeleton; all bones but 0
            //     skeletalMeshBone.Orientation.Conjugate();

            bones.Add(skeletalMeshBone);
            box.Min = skeletalMeshBone.Position.ComponentMin(box.Min);
            box.Max = skeletalMeshBone.Position.ComponentMax(box.Max);
        }
        return true;
    }

    public static bool TryConvert(this UStaticMesh originalMesh, out CStaticMesh convertedMesh)
    {
        convertedMesh = new CStaticMesh();
        if (originalMesh.RenderData == null)
            return false;

        convertedMesh.BoundingSphere = new FSphere(0f, 0f, 0f, originalMesh.RenderData.Bounds.SphereRadius / 2);
        convertedMesh.BoundingBox = new FBox(
            originalMesh.RenderData.Bounds.Origin - originalMesh.RenderData.Bounds.BoxExtent,
            originalMesh.RenderData.Bounds.Origin + originalMesh.RenderData.Bounds.BoxExtent);

        foreach (var srcLod in originalMesh.RenderData.LODs)
        {
            if (srcLod.SkipLod) continue;

            var numTexCoords = srcLod.VertexBuffer!.NumTexCoords;
            var numVerts = srcLod.PositionVertexBuffer!.Verts.Length;
            if (numVerts == 0 && numTexCoords == 0)
            {
                continue;
            }

            if (numTexCoords > Constants.MAX_MESH_UV_SETS)
                throw new ParserException($"Static mesh has too many UV sets ({numTexCoords})");

            var staticMeshLod = new CStaticMeshLod
            {
                NumTexCoords = numTexCoords,
                HasNormals = true,
                HasTangents = true,
                IsTwoSided = srcLod.CardRepresentationData?.bMostlyTwoSided ?? false,
                Indices = new Lazy<FRawStaticIndexBuffer>(srcLod.IndexBuffer!),
                Sections = new Lazy<CMeshSection[]>(() =>
                {
                    var sections = new CMeshSection[srcLod.Sections.Length];
                    for (var j = 0; j < sections.Length; j++)
                    {
                        int materialIndex = srcLod.Sections[j].MaterialIndex;
                        while (materialIndex >= originalMesh.Materials.Length)
                        {
                            materialIndex--;
                        }

                        if (materialIndex < 0) sections[j] = new CMeshSection(srcLod.Sections[j]);
                        else
                        {
                            sections[j] = new CMeshSection(materialIndex, srcLod.Sections[j],
                                originalMesh.StaticMaterials?[materialIndex].MaterialSlotName.Text, // materialName
                                originalMesh.Materials[materialIndex]); // numFaces
                        }
                    }
                    return sections;
                })
            };

            staticMeshLod.AllocateVerts(numVerts);
            if (srcLod.ColorVertexBuffer!.NumVertices != 0)
                staticMeshLod.AllocateVertexColorBuffer();

            for (var j = 0; j < numVerts; j++)
            {
                var suv = srcLod.VertexBuffer.UV[j];
                if (suv.Normal[1].Data != 0)
                    throw new ParserException("Not implemented: should only be used in UE3");

                staticMeshLod.Verts[j].Position = srcLod.PositionVertexBuffer.Verts[j];
                UnpackNormals(suv.Normal, staticMeshLod.Verts[j]);
                staticMeshLod.Verts[j].UV.U = suv.UV[0].U;
                staticMeshLod.Verts[j].UV.V = suv.UV[0].V;

                for (var k = 1; k < numTexCoords; k++)
                {
                    staticMeshLod.ExtraUV.Value[k - 1][j].U = suv.UV[k].U;
                    staticMeshLod.ExtraUV.Value[k - 1][j].V = suv.UV[k].V;
                }

                if (srcLod.ColorVertexBuffer.NumVertices != 0)
                    staticMeshLod.VertexColors![j] = srcLod.ColorVertexBuffer.Data[j];
            }

            convertedMesh.LODs.Add(staticMeshLod);
        }

        convertedMesh.FinalizeMesh();
        return true;
    }

    private static void GetBuffer(EMeshBufferSemantic bufferType, FMesh mesh, out FMeshBufferChannel? channel, out FMeshBuffer? vertexBuffer, int semanticIndex = 0)
    {
        foreach (var buffer in mesh.IndexBuffers.Buffers)
        {
            channel = buffer.Channels.FirstOrDefault(c => c.Semantic == bufferType && c.SemanticIndex == semanticIndex);
            if (channel is null) continue;

            vertexBuffer = buffer;
            return;
        }

        foreach (var buffer in mesh.VertexBuffers.Buffers)
        {
            channel = buffer.Channels.FirstOrDefault(c => c.Semantic == bufferType && c.SemanticIndex == semanticIndex);
            if (channel is null) continue;

            vertexBuffer = buffer;
            return;
        }

        channel = null;
        vertexBuffer = null;
    }

    private static void UnpackNormals(FPackedNormal[] normal, CMeshVertex v)
    {
        // tangents: convert to FVector (unpack) then cast to CVec3
        v.Tangent = normal[0];
        v.Normal = normal[2];

        // new UE3 version - binormal is not serialized and restored in vertex shader
        if (normal[1] is not null && normal[1].Data != 0)
        {
            throw new NotImplementedException();
        }
    }
}
