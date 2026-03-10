using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using CUE4Parse.UE4.Assets.Exports.CustomizableObject;
using CUE4Parse.UE4.Readers;

namespace CUE4Parse.UE4.Assets.Readers;

public class FMutableArchive : FArchive
{
    private readonly FArchive _baseArchive;
    
    /// <summary> Maximum array length when reading TArray from Mutable blobs to avoid overflow from misaligned/garbage lengths. </summary>
    private const int MaxArrayLength = 256 * 1024;
    
    public FMutableArchive(FArchive baseArchive)
    {
        _baseArchive = baseArchive;
        Versions = baseArchive.Versions;
    }
    
    public override int Read(byte[] buffer, int offset, int count) => _baseArchive.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => _baseArchive.Seek(offset, origin);
    public override string ReadFString() => new string(_baseArchive.ReadArray<char>()).Replace("\0", string.Empty);
    
    /// <summary>
    /// Mutable serializes FString as TArray&lt;TCHAR&gt;: int32 length then length*sizeof(TCHAR) bytes (UTF-16 LE on Windows).
    /// Use this for physics body names and other Mutable FString fields; standard ReadFString uses 1 byte per char and misaligns.
    /// </summary>
    public string ReadMutableFString()
    {
        var length = _baseArchive.Read<int>();
        if (length <= 0) return string.Empty;
        if (length > 65536) length = 65536; // guard against corrupted/garbage length
        var bytes = _baseArchive.ReadBytes(length * 2);
        return Encoding.Unicode.GetString(bytes).Replace("\0", string.Empty);
    }
    
    public override T[] ReadArray<T>(Func<T> getter)
    {
        var length = _baseArchive.Read<int>();
        if (length <= 0) return [];
        if (length > MaxArrayLength) length = MaxArrayLength;
        return _baseArchive.ReadArray(length, getter);
    }

    public override T[] ReadArray<T>() where T : struct
    {
        var length = _baseArchive.Read<int>();
        if (length <= 0) return [];
        if (length > MaxArrayLength) length = MaxArrayLength;
        var elemSize = Unsafe.SizeOf<T>();
        var remaining = _baseArchive.Length - _baseArchive.Position;
        if (remaining < (long)length * elemSize && remaining >= 0)
            length = (int)Math.Max(0, remaining / elemSize);
        return length > 0 ? _baseArchive.ReadArray<T>(length) : [];
    }
    
    public T ReadPtr<T>() where T : unmanaged => _baseArchive.Read<int>() == -1 ? default : _baseArchive.Read<T>();
    public T? ReadPtr<T>(Func<T> getter) where T : class => _baseArchive.Read<int>() == -1 ? null : getter();
    public T[] ReadPtrArray<T>(Func<T> getter)
    {
        var length = _baseArchive.Read<int>();
        if (length <= 0) return [];
        if (length > MaxArrayLength) length = MaxArrayLength;

        var list = new List<T>(length);
        for (var i = 0; i < length; i++)
        {
            var id = _baseArchive.Read<int>();
            if (id == -1) continue;
            
            list.Add(getter());
        }

        return list.ToArray();
    }

    /// <summary>
    /// Read TArray of TManagedPtr like Unreal: each element is int32 id; id==-1 means null/skip.
    /// When id was already seen we reuse the same instance (no read); first occurrence reads from stream.
    /// Use for ConstantMeshesPermanent, ConstantImageLODsPermanent, etc. so stream alignment is correct.
    /// </summary>
    public T[] ReadPtrArrayWithHistory<T>(Func<T> getter) where T : class
    {
        var length = _baseArchive.Read<int>();
        if (length <= 0) return [];
        if (length > MaxArrayLength) length = MaxArrayLength;
        var history = new Dictionary<int, T>();
        var list = new List<T>(length);
        for (var i = 0; i < length; i++)
        {
            var id = _baseArchive.Read<int>();
            if (id == -1) continue;
            if (history.TryGetValue(id, out var existing))
            {
                list.Add(existing);
                continue;
            }
            var item = getter();
            history[id] = item;
            list.Add(item);
        }
        return list.ToArray();
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

    public override bool CanSeek => _baseArchive.CanSeek;
    public override long Length => _baseArchive.Length;
    public override string Name  => _baseArchive.Name;
    public override long Position
    {
        get => _baseArchive.Position;
        set => _baseArchive.Position = value;
    }

    public override object Clone() => new FMutableArchive(_baseArchive);
}
