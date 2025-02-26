using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;

namespace CUE4Parse.UE4.Readers;

public class FMutableArchive : FArchive
{
    private readonly FArchive InnerArchive;

    public FMutableArchive(FArchive innerArchive, VersionContainer? versionContainer = null) : base(versionContainer)
    {
        InnerArchive = innerArchive;
    }

    public override int Read(byte[] buffer, int offset, int count) => InnerArchive.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => InnerArchive.Seek(offset, origin);

    public override bool CanSeek => InnerArchive.CanSeek;
    public override long Length => InnerArchive.Length;
    public override string Name => InnerArchive.Name;

    public override long Position
    {
        get => InnerArchive.Position;
        set => InnerArchive.Position = value;
    }

    public override FName ReadFName() => new FName(ReadFString());
    public override string ReadFString()
    {
        var length = Read<int>() * 2; // one char occupies two bytes
        string value;

        if (length == int.MinValue)
            throw new ArgumentOutOfRangeException(nameof(length), "Archive is corrupted");

        if (Math.Abs(length) > Length - Position)
            throw new ParserException($"Invalid FString length '{length}'");

        switch (length)
        {
            case < 0:
            {
                unsafe
                {
                    length = -length;
                    var ucs2Length = length * sizeof(ushort);
                    var ucs2Bytes = ucs2Length <= 1024 ? stackalloc byte[ucs2Length] : new byte[ucs2Length];
                    fixed (byte* ucs2BytesPtr = ucs2Bytes)
                    {
                        Serialize(ucs2BytesPtr, ucs2Length);
#if !NO_STRING_NULL_TERMINATION_VALIDATION
                        if (ucs2Bytes[ucs2Length - 1] != 0 || ucs2Bytes[ucs2Length - 2] != 0)
                        {
                            throw new ParserException(this, "Serialized FString is not null terminated");
                        }
#endif
                        value = new string((char*) ucs2BytesPtr, 0 , length - 1);
                    }
                }

                break;
            }
            case > 0:
            {
                unsafe
                {
                    var ansiBytes = length <= 2024 ? stackalloc byte[length] : new byte[length];
                    fixed (byte* ansiBytesPtr = ansiBytes)
                    {
                        Serialize(ansiBytesPtr, length);
#if !NO_STRING_NULL_TERMINATION_VALIDATION
                        if (ansiBytes[length - 1] != 0)
                        {
                            throw new ParserException(this, "Serialized FString is not null terminated");
                        }
#endif
                        value = new string((sbyte*) ansiBytesPtr, 0, length - 1);
                    }
                }

                break;
            }
            default:
            {
                value = string.Empty;
                break;
            }
        }

        return value.Replace("\0", string.Empty);
    }

    public T[] ReadPtrArray<T>(Func<T> getter)
    {
        var length = Read<int>();
        var array = new T[length];

        for (int i = 0; i < length; i++)
        {
            var id = Read<int>();
            if (id == -1)
            {
                i--;
                continue;
            }

            array[i] = getter();
        }

        return array;
    }

    public T ReadPtr<T>(Func<T> getter) where T : class
    {
        var id = Read<int>();
        return id == -1 ? null : getter();
    }

    public override object Clone() => InnerArchive.Clone();
}
