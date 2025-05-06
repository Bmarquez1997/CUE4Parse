using System.Runtime.InteropServices;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Image;

[StructLayout(LayoutKind.Sequential)]
public struct FImageLODRange
{
    public int FirstIndex;
    public ushort ImageSizeX;
    public ushort ImageSizeY;
    public ushort _Padding;
    public byte LODCount;
    public EImageFormat ImageFormat;
}
