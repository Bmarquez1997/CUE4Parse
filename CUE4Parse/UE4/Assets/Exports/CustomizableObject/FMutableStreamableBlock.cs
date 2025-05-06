using System;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject;

[JsonConverter(typeof(FMutableStreamableBlockConverter))]
public class FMutableStreamableBlock
{
    public uint FileId;
    public EMutableFileFlags Flags;
    public ulong Offset;

    public FMutableStreamableBlock(FAssetArchive Ar)
    {
        FileId = Ar.Read<uint>();
        Flags = (EMutableFileFlags)(Ar.Game >= EGame.GAME_UE5_6 ? Ar.Read<ushort>() : Ar.Read<uint>()); // This should be PrefetchHighQualityMipMaps
        Offset = Ar.Read<ulong>();
    }
}

[Flags]
public enum EMutableFileFlags : byte
{
    None	= 0,
    HighRes = 1 << 0,
}
