using CUE4Parse.UE4.Readers;

namespace CUE4Parse.UE4.Assets.Exports.SkeletalMesh;

public class FClothBufferIndexMapping
{
    public uint BaseVertexIndex;
    public uint MappingOffset;
    public uint LODBiasStride;

    public FClothBufferIndexMapping(FArchive Ar)
    {
        BaseVertexIndex = Ar.Read<uint>();
        MappingOffset = Ar.Read<uint>();
        LODBiasStride = Ar.Read<uint>();
    }
}