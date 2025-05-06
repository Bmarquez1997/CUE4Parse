using System;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Image;

namespace CUE4Parse_Conversion.Textures.RLE;

public static class RLEDecoder
{
    public static byte[] UncompressRLE_L(FImage BaseImage, byte[] baseData)
    {
        int sizeX = BaseImage.DataStorage.ImageSize.X;
        int sizeY = BaseImage.DataStorage.ImageSize.Y;

        var numLODs = MathF.Max(1, BaseImage.DataStorage.NumLODs);
        for (int i = 0; i < numLODs; i++)
        {

        }

        return [];
    }

    public static byte[] UncompressRLE_R(int width, int rows, byte[] baseData)
    {
        byte[] destData = new byte[width * rows];

        int pBaseData = sizeof(uint); // Total mip size
        pBaseData += rows * sizeof(uint); // Size of each row

        int destDataOffset = 0;

        for (int r = 0; r < rows; ++r)
        {
            int destRowEnd = destDataOffset + width;

            while (destDataOffset != destRowEnd)
            {
                // Decode header
                ushort equal = BitConverter.ToUInt16(baseData, pBaseData);
                pBaseData += 2;

                byte different = baseData[pBaseData];
                ++pBaseData;

                byte equalPixel = baseData[pBaseData];
                ++pBaseData;

                if (equal > 0)
                {

                    Array.Fill(destData, equalPixel, destDataOffset, equal);
                    destDataOffset += equal;
                }

                if (different > 0)
                {
                    Buffer.BlockCopy(baseData, pBaseData, destData, destDataOffset, different);
                    destDataOffset += different;
                    pBaseData += different;
                }
            }
        }

        uint totalSize = (uint)(pBaseData);
        uint expectedSize = BitConverter.ToUInt32(baseData, 0);

        if (totalSize != expectedSize)
        {
            throw new InvalidOperationException("Data size mismatch.");
        }

        return destData;
    }
}
