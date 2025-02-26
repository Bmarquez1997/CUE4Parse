using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Meshes;
using CUE4Parse.UE4.Objects.RenderCore;

namespace CUE4Parse_Conversion.Meshes;

public class MutableDataConverter
{
    private readonly uint _indexBufferElementCount;

    public MutableDataConverter(uint indexBufferElementCount)
    {
        _indexBufferElementCount = indexBufferElementCount;
    }

    public uint[] GetIndices(FMeshBufferChannel? channel, FMeshBuffer? indexBuffer)
    {
        if (channel == null || indexBuffer == null)
            throw new ArgumentNullException();

        var indices = new uint[_indexBufferElementCount];

        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = channel.Format switch
            {
                EMeshBufferFormat.UInt32 => BitConverter.ToUInt32(indexBuffer.Data, i * (int)indexBuffer.ElementSize + channel.Offset),
                _ => throw new NotImplementedException($"Format {channel.Format} is currently not supported")
            };
        }

        if (indices.Length != _indexBufferElementCount)
            throw new ArgumentException("indices.Length != IndexBufferElementCount");

        return indices;
    }

    public FVector GetVertices(FMeshBufferChannel? channel, FMeshBuffer? vertexBuffer, int index)
    {
        if (channel == null || vertexBuffer == null)
            throw new ArgumentNullException();

        switch (channel.Format)
        {
            case EMeshBufferFormat.Float32:
            {
                var x = BitConverter.ToSingle(vertexBuffer.Data, index * (int)vertexBuffer.ElementSize + channel.Offset);
                var y = BitConverter.ToSingle(vertexBuffer.Data, index * (int)vertexBuffer.ElementSize + channel.Offset + 4);
                var z = BitConverter.ToSingle(vertexBuffer.Data, index * (int)vertexBuffer.ElementSize + channel.Offset + 8);

                return new FVector(x, y, z);
            }
            default:
                throw new NotImplementedException($"Format {channel.Format} is currently not supported");
        }
    }

    public FPackedNormal GetNormals(FMeshBufferChannel? channel, FMeshBuffer? vertexBuffer, int index)
    {
        if (channel == null || vertexBuffer == null)
            throw new ArgumentNullException();

        switch (channel.Format)
        {
            case EMeshBufferFormat.PackedDirS8_W_TangentSign:
            {
                float x = vertexBuffer.Data[index * (int)vertexBuffer.ElementSize + channel.Offset + 0] / 127.5f;
                float y = vertexBuffer.Data[index * (int)vertexBuffer.ElementSize + channel.Offset + 1] / 127.5f;
                float z = vertexBuffer.Data[index * (int)vertexBuffer.ElementSize + channel.Offset + 2] / 127.5f;
                float w = vertexBuffer.Data[index * (int)vertexBuffer.ElementSize + channel.Offset + 3] / 127.5f;

                return new FPackedNormal(new FVector4(x, y, z, w));
            }
            case EMeshBufferFormat.Float32:
            {
                float x = BitConverter.ToSingle(vertexBuffer.Data, index * (int)vertexBuffer.ElementSize + channel.Offset);
                float y = BitConverter.ToSingle(vertexBuffer.Data, index * (int)vertexBuffer.ElementSize + channel.Offset + 4);
                float z = BitConverter.ToSingle(vertexBuffer.Data, index * (int)vertexBuffer.ElementSize + channel.Offset + 8);

                return new FPackedNormal(new FVector(x, y, z));
            }
            default:
                throw new NotImplementedException($"Format {channel.Format} is currently not supported");
        }
    }

    public FPackedNormal GetTangent(FMeshBufferChannel? channel, FMeshBuffer? vertexBuffer, int index)
    {
        if (channel == null || vertexBuffer == null)
            throw new ArgumentNullException();

        switch (channel.Format)
        {
            case EMeshBufferFormat.PackedDirS8:
            case EMeshBufferFormat.PackedDirS8_W_TangentSign:
            {
                float x = vertexBuffer.Data[index * (int)vertexBuffer.ElementSize + channel.Offset + 0] / 127.5f;
                float y = vertexBuffer.Data[index * (int)vertexBuffer.ElementSize + channel.Offset + 1] / 127.5f;
                float z = vertexBuffer.Data[index * (int)vertexBuffer.ElementSize + channel.Offset + 2] / 127.5f;
                float w = vertexBuffer.Data[index * (int)vertexBuffer.ElementSize + channel.Offset + 3] / 127.5f;

                return new FPackedNormal(new FVector4(x, y, z, w));
            }
            case EMeshBufferFormat.Float32:
            {
                float x = BitConverter.ToSingle(vertexBuffer.Data, index * (int)vertexBuffer.ElementSize + channel.Offset);
                float y = BitConverter.ToSingle(vertexBuffer.Data, index * (int)vertexBuffer.ElementSize + channel.Offset + 4);
                float z = BitConverter.ToSingle(vertexBuffer.Data, index * (int)vertexBuffer.ElementSize + channel.Offset + 8);

                return new FPackedNormal(new FVector(x, y, z));
            }
            default:
                throw new NotImplementedException($"Format {channel.Format} is currently not supported");
        }
    }

    public FMeshUVFloat GetUVs(FMeshBufferChannel? channel, FMeshBuffer? vertexBuffer, int index)
    {
        if (channel == null || vertexBuffer == null)
            throw new ArgumentNullException();

        switch (channel.Format)
        {
            case EMeshBufferFormat.Float32:
            {
                var u = BitConverter.ToSingle(vertexBuffer.Data, index * (int)vertexBuffer.ElementSize + channel.Offset);
                var v = BitConverter.ToSingle(vertexBuffer.Data, index * (int)vertexBuffer.ElementSize + channel.Offset + 4);

                return new FMeshUVFloat(u, v);
            }
            default:
                throw new NotImplementedException($"Format {channel.Format} is currently not supported");
        }
    }
    
    public FColor GetColor(FMeshBufferChannel? channel, FMeshBuffer? vertexBuffer, int index)
    {
        if (channel == null || vertexBuffer == null)
            throw new ArgumentNullException();

        switch (channel.Format)
        {
            case EMeshBufferFormat.NUInt8:
            {
                var colorB = vertexBuffer.Data[index * ((int)vertexBuffer.ElementSize) + channel.Offset];
                var colorG = vertexBuffer.Data[index * ((int)vertexBuffer.ElementSize) + channel.Offset + 1];
                var colorR = vertexBuffer.Data[index * ((int)vertexBuffer.ElementSize) + channel.Offset + 2];
                var colorA = vertexBuffer.Data[index * ((int)vertexBuffer.ElementSize) + channel.Offset + 3];

                return new FColor(colorR, colorG, colorB, colorA);
            }
            default:
                throw new NotImplementedException($"Format {channel.Format} is currently not supported");
        }
    }
    
    public List<Tuple<short, byte>> GetWeights(FMeshBuffer? weightBuffer, FMeshBufferChannel? boneIndexChannel, FMeshBufferChannel? weightChannel, int vertIndex)
     {
         if (weightChannel == null || weightBuffer == null || boneIndexChannel == null)// || boneIndexBuffer == null)
             throw new ArgumentNullException();

         if (weightChannel.Format == EMeshBufferFormat.NUInt8 && boneIndexChannel.Format == EMeshBufferFormat.UInt8)
         {
             List<Tuple<short, byte>> weightList = [];

             for (int i = 0; i < boneIndexChannel.ComponentCount; i++)
             {
                 var boneIndex =
                     weightBuffer.Data[vertIndex * ((int)weightBuffer.ElementSize) + boneIndexChannel.Offset + i];
                 var weight = weightBuffer.Data[vertIndex * ((int)weightBuffer.ElementSize) + weightChannel.Offset + i];
                 weightList.Add(new Tuple<short, byte>(boneIndex, weight));
             }

             return weightList;
         }
         
         throw new NotImplementedException($"Format combination (boneIndex: {boneIndexChannel.Format}, weight: {weightChannel.Format}) is currently not supported");
     }
}
