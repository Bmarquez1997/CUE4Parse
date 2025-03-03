using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Versions;

namespace CUE4Parse.UE4.Readers
{
    public class FArchiveBigEndian : FArchive
    {
        private readonly FArchive _Ar;

        public override string Name => _Ar.Name;

        public override bool CanSeek => _Ar.CanSeek;

        public override long Length => _Ar.Length;

        public override long Position { get => _Ar.Position; set => _Ar.Position = value; }

        public FArchiveBigEndian(FArchive Ar)
        {
            _Ar = Ar;
        }

        public override object Clone()
        {
            return _Ar.Clone();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _Ar.Read(buffer, offset, count);
        }

        private static readonly Dictionary<Type, Delegate> _read = new Dictionary<Type, Delegate>()
        {
            { typeof(short), (FArchiveBigEndian Ar) => BinaryPrimitives.ReadInt16BigEndian(Ar.ReadBytes(sizeof(short))) },
            { typeof(int), (FArchiveBigEndian Ar) => BinaryPrimitives.ReadInt32BigEndian(Ar.ReadBytes(sizeof(int))) },
            { typeof(long), (FArchiveBigEndian Ar) => BinaryPrimitives.ReadInt64BigEndian(Ar.ReadBytes(sizeof(long))) },

            { typeof(ushort), (FArchiveBigEndian Ar) => BinaryPrimitives.ReadUInt16BigEndian(Ar.ReadBytes(sizeof(ushort))) },
            { typeof(uint), (FArchiveBigEndian Ar) => BinaryPrimitives.ReadUInt32BigEndian(Ar.ReadBytes(sizeof(uint))) },
            { typeof(ulong), (FArchiveBigEndian Ar) => BinaryPrimitives.ReadUInt64BigEndian(Ar.ReadBytes(sizeof(ulong))) },

            { typeof(float), (FArchiveBigEndian Ar) => BinaryPrimitives.ReadSingleBigEndian(Ar.ReadBytes(sizeof(float))) },
            { typeof(double), (FArchiveBigEndian Ar) => BinaryPrimitives.ReadDoubleBigEndian(Ar.ReadBytes(sizeof(double))) },
        };

        public override string ReadString()
        {
            return Encoding.ASCII.GetString(ReadArray<byte>());
        }

        public override T[] ReadArray<T>()
        {
            if (_read.TryGetValue(typeof(T), value: out var func))
                return base.ReadArray(() => ((Func<FArchiveBigEndian, T>) func)(this));
            return base.ReadArray<T>();
        }

        public override T Read<T>()
        {
            if (_read.TryGetValue(typeof(T), value: out var func))
                return ((Func<FArchiveBigEndian, T>) func)(this);
            return base.Read<T>();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _Ar.Seek(offset, origin);
        }
    }
}