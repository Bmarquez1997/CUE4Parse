using System.Runtime.InteropServices;

namespace CUE4Parse.UE4.IO.Objects
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct FBulkDataMapEntry
    {
        public const uint Size = 32;

        public readonly ulong SerialOffset;
        public readonly ulong DuplicateSerialOffset;
        public readonly ulong SerialSize;
        public readonly uint Flags;
        public readonly byte CookedIndex; // https://github.com/EpicGames/UnrealEngine/commit/6e7f2558611221cfdf413106900caf947e3c17c5
        public readonly short _pad0;
        public readonly byte _pad1;
    }
}