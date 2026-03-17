using System.Runtime.InteropServices;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Images;

[StructLayout(LayoutKind.Sequential)]
public struct FImageLODRange
{
    public int FirstIndex;
    public ushort ImageSizeX;
    public ushort ImageSizeY;
    public byte LODCount; // Actual LOD count seems to be (LODCount - NumLODsInTail) + 1.  Ex: LODCount = 10, NumLODsInTail = 7, Actual LOD count = 4
    public byte NumLODsInTail;
    public byte Flags;
    public EImageFormat ImageFormat;
}