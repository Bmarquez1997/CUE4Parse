using System;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;

namespace CUE4Parse.UE4.Assets.Exports.SkeletalMesh
{
    public class FMultisizeIndexContainer
    {
        public ushort[] Indices16;
        public uint[] Indices32;

        public FMultisizeIndexContainer()
        {
            Indices16 = [];
            Indices32 = [];
        }
        
        public FMultisizeIndexContainer(FArchive Ar) : this()
        {
            if (Ar.Ver < EUnrealEngineObjectUE4Version.KEEP_SKEL_MESH_INDEX_DATA)
                _ = Ar.ReadBoolean(); // bOldNeedsCPUAccess

            var dataTypeSize = Ar.Read<byte>();
            if (Ar.Game == EGame.GAME_OutlastTrials) Ar.Position += 4;

            if (dataTypeSize == 0x02)
            {
                Indices16 = Ar.ReadBulkArray<ushort>();
            }
            else
            {
                Indices32 = Ar.ReadBulkArray<uint>();
            }
        }
        
        public static int CalcMetaDataSize()
        {
            var numBytes = 0;

            numBytes += 1; // dataTypeSize
            numBytes += 4; // cachedNumIndices

            return numBytes;
        }
    }
}
