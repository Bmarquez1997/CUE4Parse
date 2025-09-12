using System;
using System.IO;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;

namespace CUE4Parse.UE4.Assets.Readers;

public class FMutableArchive : FArchive
{
    private readonly FArchive _baseArchive;

    public FMutableArchive(FArchive baseArchive) : base(baseArchive.Versions)
    {
        _baseArchive = baseArchive;
    }

    public override string Name => _baseArchive.Name;
    public override bool CanSeek => _baseArchive.CanSeek;
    public override long Length => _baseArchive.Length;
    public override long Position
    {
        get => _baseArchive.Position;
        set => _baseArchive.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count) => _baseArchive.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => _baseArchive.Seek(offset, origin);
    public override object Clone() => new FMutableArchive(_baseArchive);
    public override string ReadFString() => new string(ReadArray<char>()).Replace("\0", string.Empty);

    public T[] ReadPtrArray<T>(Func<T> getter)
    {
        var length = Read<int>();
        if (length == 0)
            return [];

        var array = new T[length];

        for (int i = 0; i < length; i++)
        {
            var id = Read<int>();
            if (id == -1)
                continue;

            array[i] = getter();
        }

        return array;
    }

    public T? ReadPtr<T>()
    {
        var id = Read<int>();
        return id == -1 ? default : Read<T>();
    }

    public T? ReadPtr<T>(Func<T> getter)
    {
        var id = Read<int>();
        return id == -1 ? default : getter();
    }

    public TVariant<T1, T2>? ReadTVariant<T1, T2>(Func<T1> getter1, Func<T2> getter2)
    {
        var variantIndex = Read<byte>();

        return variantIndex switch
        {
            0 => new TVariant<T1, T2>(getter1()),
            1 => new TVariant<T1, T2>(getter2()),
            _ => throw new IndexOutOfRangeException($"Index {variantIndex} out of bounds")
        };
    }
}
