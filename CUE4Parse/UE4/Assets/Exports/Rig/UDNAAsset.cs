using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using CUE4Parse.UE4.Assets.Exports.Animation.CurveExpression;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.Rig
{
    public class UDNAAsset : UObject
    {
        readonly public byte[] Signature = { (byte)'D', (byte) 'N', (byte)'A' };
        readonly public byte[] Eof = { (byte) 'A', (byte) 'N', (byte) 'D' };
        public FDNAAssetCustomVersion.Type DnaFileVersion = FDNAAssetCustomVersion.Type.LatestVersion;
        public string DnaFileName { get; private set; }
        public bool bKeepDNAAfterInitialization;
        public Dictionary<string, string> MetaData;
        public FRigLogicConfiguration RigLogicConfiguration;

        public IDNAReader BehaviorReader;
        public IDNAReader GeometryReader;
        public FSharedRigRuntimeContext RigRuntimeContext;
        public Dictionary<FWeakObjectProperty, FDNAIndexMapping> DNAIndexMappingContainer;

        public RawDescriptor Descriptor;
        public RawDefinition Definition;
        public RawBehavior Behavior;

        private void DeserializeDNA(FArchiveBigEndian Ar)
        {
            // Signature
            var offsetStart = Ar.Position;
            if (!Ar.ReadBytes(3).SequenceEqual(Signature))
                return;

            // Version
            DnaFileVersion = FDNAAssetCustomVersion.Get(Ar);

            // v21
            // TODO: Other file versions
            // sections
            var sections = new SectionLookupTable(Ar, offsetStart);

            // descriptor
            // TODO: Only seek based on condition of file version
            Ar.Seek(sections.OffsetStart + sections.Descriptor, System.IO.SeekOrigin.Begin);
            Descriptor = new RawDescriptor(Ar);

            // definition
            Ar.Seek(sections.OffsetStart + sections.Definition, System.IO.SeekOrigin.Begin);
            Definition = new RawDefinition(Ar);

            // behavior
            Ar.Seek(sections.OffsetStart + sections.Behavior, System.IO.SeekOrigin.Begin);
            Behavior = new RawBehavior(Ar);

            // geometry (unimplemented)
            // TODO: Implement geom parsing
            Ar.Seek(sections.OffsetStart + sections.Geometry, System.IO.SeekOrigin.Begin);
            Ar.ReadBytes(4);

            // eof check
            // TODO: convert to exception
            if (!Ar.ReadBytes(3).SequenceEqual(Eof))
                Console.WriteLine("ERROR: invalid end of DNA file");
        }

        // Endianness - Big / Network
        // TSize = uint32t -- 4byte alignment
        // TOffset = uint32t
        public override void Deserialize(FAssetArchive Ar, long validPos)
        {
            File.WriteAllBytes("B:/workspace_fp/jones.dna", Ar.GetBytes());
            base.Deserialize(Ar, validPos);
            DnaFileName = GetOrDefault<string>(nameof(DnaFileName));
            if (FDNAAssetCustomVersion.Get(Ar) >= FDNAAssetCustomVersion.Type.BeforeCustomVersionWasAdded)
            {
                DeserializeDNA(new FArchiveBigEndian(Ar));
            }
        }

        protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
        {
            base.WriteJson(writer, serializer);

            writer.WritePropertyName(nameof(Descriptor));
            serializer.Serialize(writer, Descriptor);

            writer.WritePropertyName(nameof(Definition));
            serializer.Serialize(writer, Definition);

            writer.WritePropertyName(nameof(Behavior));
            serializer.Serialize(writer, Behavior);
        }
    }
    
    public enum DNAFileVersion : uint
    {
        Unknown = 0,
        v21 = (2 << 16) + 1, // rev(2, 1),
        v22 = (2 << 16) + 2, // rev(2, 2)
        v23 = (2 << 16) + 3, // rev(2, 3)
        Latest = v23,
    }

    struct DNAVersion
    {
        public readonly ushort Generation;
        public readonly ushort Version;

        public DNAVersion(FArchiveBigEndian Ar)
        {
            Generation = Ar.Read<ushort>();
            Version = Ar.Read<ushort>();
        }

        public DNAFileVersion FileVersion()
        {
            return (DNAFileVersion)((Generation << 16) + Version);
        }
    }

    public struct SectionLookupTable
    {
        public readonly uint Descriptor;
        public readonly uint Definition;
        public readonly uint Behavior;
        public readonly uint Controls;
        public readonly uint Joints;
        public readonly uint BlendShapeChannels;
        public readonly uint AnimatedMaps;
        public readonly uint Geometry;

        public readonly long OffsetStart;

        public SectionLookupTable(FArchiveBigEndian Ar, long start)
        {
            Descriptor = Ar.Read<uint>();
            Definition = Ar.Read<uint>();
            Behavior = Ar.Read<uint>();
            Controls = Ar.Read<uint>();
            Joints = Ar.Read<uint>();
            BlendShapeChannels = Ar.Read<uint>();
            AnimatedMaps = Ar.Read<uint>();
            Geometry = Ar.Read<uint>();

            OffsetStart = start;
        }
    }

    public struct RawCoordinateSystem
    {
        public readonly ushort XAxis;
        public readonly ushort YAxis;
        public readonly ushort ZAxis;

        public RawCoordinateSystem(FArchiveBigEndian Ar)
        {
            XAxis = Ar.Read<ushort>();
            YAxis = Ar.Read<ushort>();
            ZAxis = Ar.Read<ushort>();
        }
    }

    public struct MetadataPair
    {
        public readonly string Key;
        public readonly string Value;

        public MetadataPair(FArchiveBigEndian Ar)
        {
            Key = Ar.ReadString();
            Value = Ar.ReadString();
        }
    }

    public struct RawDescriptor
    {
        public readonly string Name;
        public readonly ushort Archetype;
        public readonly ushort Gender;
        public readonly ushort Age;
        public readonly MetadataPair[] Metadata;
        public readonly ushort TranslationUnit;
        public readonly ushort RotationUnit;
        public readonly RawCoordinateSystem CoordinateSystem;
        public readonly ushort LodCount;
        public readonly ushort MaxLod;
        public readonly string Complexity;
        public readonly string DbName;

        public RawDescriptor(FArchiveBigEndian Ar)
        {
            Name = Ar.ReadString();
            Archetype = Ar.Read<ushort>();
            Gender = Ar.Read<ushort>();
            Age = Ar.Read<ushort>();
            Metadata = Ar.ReadArray(() => new MetadataPair(Ar));
            TranslationUnit = Ar.Read<ushort>();
            RotationUnit = Ar.Read<ushort>();
            // TODO: Ar.Read<RawCoordinateSystem>() results in WRONG VALUES, maybe do not extend from FArchive???
            CoordinateSystem = new RawCoordinateSystem(Ar);
            LodCount = Ar.Read<ushort>();
            MaxLod = Ar.Read<ushort>();
            Complexity = Ar.ReadString();
            DbName = Ar.ReadString();
        }
    }

    public struct RawLODMapping {
        public readonly ushort[] Lods;
        public readonly ushort[][] Indices;

        public RawLODMapping(FArchiveBigEndian Ar)
        {
            Lods = Ar.ReadArray<ushort>();
            Indices = Ar.ReadArray(Ar.ReadArray<ushort>);
        }

    }

    public struct RawVector3Vector
    {
        public readonly float[] Xs;
        public readonly float[] Ys;
        public readonly float[] Zs;

        public RawVector3Vector(FArchiveBigEndian Ar)
        {
            Xs = Ar.ReadArray<float>();
            Ys = Ar.ReadArray<float>();
            Zs = Ar.ReadArray<float>();
        }
    }

    public struct RawSurjectiveMapping<TFrom, TTo> {
        public readonly TFrom[] From;
        public readonly TTo[] To;

        public RawSurjectiveMapping(FArchiveBigEndian Ar)
        {
            From = Ar.ReadArray(Ar.Read<TFrom>);
            To = Ar.ReadArray(Ar.Read<TTo>);
        }
    }

    public struct RawDefinition
    {
        public readonly RawLODMapping LodJointMapping;
        public readonly RawLODMapping LodBlendShapeMapping;
        public readonly RawLODMapping LodAnimatedMapMapping;
        public readonly RawLODMapping LodMeshMapping;
        public readonly string[] GuiControlNames;
        public readonly string[] RawControlNames;
        public readonly string[] JointNames;
        public readonly string[] BlendShapeChannelNames;
        public readonly string[] AnimatedMapNames;
        public readonly string[] MeshNames;
        public readonly RawSurjectiveMapping<ushort, ushort> MeshBlendShapeChannelMapping;
        public readonly ushort[] JointHierarchy;
        public readonly RawVector3Vector NeutralJointTranslations;
        public readonly RawVector3Vector NeutralJointRotations;

        public RawDefinition(FArchiveBigEndian Ar)
        {
            LodJointMapping = new RawLODMapping(Ar);
            LodBlendShapeMapping = new RawLODMapping(Ar);
            LodAnimatedMapMapping = new RawLODMapping(Ar);
            LodMeshMapping = new RawLODMapping(Ar);
            GuiControlNames = Ar.ReadArray(Ar.ReadString);
            RawControlNames = Ar.ReadArray(Ar.ReadString);
            JointNames = Ar.ReadArray(Ar.ReadString);
            BlendShapeChannelNames = Ar.ReadArray(Ar.ReadString);
            AnimatedMapNames = Ar.ReadArray(Ar.ReadString);
            MeshNames = Ar.ReadArray(Ar.ReadString);
            MeshBlendShapeChannelMapping = new RawSurjectiveMapping<ushort, ushort>(Ar);
            JointHierarchy = Ar.ReadArray<ushort>();
            NeutralJointTranslations = new RawVector3Vector(Ar);
            NeutralJointRotations = new RawVector3Vector(Ar);
        }
    }

    public struct RawConditionalTable
    {
        public readonly ushort[] InputIndices;
        public readonly ushort[] OutputIndices;
        public readonly float[] FromValues;
        public readonly float[] ToValues;
        public readonly float[] SlopeValues;
        public readonly float[] CutValues;

        public RawConditionalTable(FArchiveBigEndian Ar)
        {
            InputIndices = Ar.ReadArray<ushort>();
            OutputIndices = Ar.ReadArray<ushort>();
            FromValues = Ar.ReadArray<float>();
            ToValues = Ar.ReadArray<float>();
            SlopeValues = Ar.ReadArray<float>();
            CutValues = Ar.ReadArray<float>();
        }
    }

    public struct RawPSDMatrix
    {
        public readonly ushort[] Rows;
        public readonly ushort[] Columns;
        public readonly float[] Values;

        public RawPSDMatrix(FArchiveBigEndian Ar)
        {
            Rows = Ar.ReadArray<ushort>();
            Columns = Ar.ReadArray<ushort>();
            Values = Ar.ReadArray<float>();
        }
    }

    public struct RawControls
    {
        public readonly ushort PsdCount;
        public readonly RawConditionalTable Conditionals;
        public readonly RawPSDMatrix Psds;

        public RawControls(FArchiveBigEndian Ar)
        {
            PsdCount = Ar.Read<ushort>();
            Conditionals = new RawConditionalTable(Ar);
            Psds = new RawPSDMatrix(Ar);
        }
    }

    public struct RawJointGroups
    {
        public readonly ushort[] Lods;
        public readonly ushort[] InputIndices;
        public readonly ushort[] OutputIndices;
        public readonly float[] Values;
        public readonly ushort[] JointInidices;

        public RawJointGroups(FArchiveBigEndian Ar)
        {
            Lods = Ar.ReadArray<ushort>();
            InputIndices = Ar.ReadArray<ushort>();
            OutputIndices = Ar.ReadArray<ushort>();
            Values = Ar.ReadArray<float>();
            JointInidices = Ar.ReadArray<ushort>();
        }
    }

    public struct RawJoints
    {
        public readonly ushort RowCount;
        public readonly ushort ColCount;
        public readonly RawJointGroups[] JointGroups;

        public RawJoints(FArchiveBigEndian Ar)
        {
            RowCount = Ar.Read<ushort>();
            ColCount = Ar.Read<ushort>();
            JointGroups = Ar.ReadArray(() => new RawJointGroups(Ar));
        }
    }

    public struct RawBlendShapeChannels
    {
        public readonly ushort[] Lods;
        public readonly ushort[] InputIndices;
        public readonly ushort[] OutputIndices;

        public RawBlendShapeChannels(FArchiveBigEndian Ar)
        {
            Lods = Ar.ReadArray<ushort>();
            InputIndices = Ar.ReadArray<ushort>();
            OutputIndices = Ar.ReadArray<ushort>();
        }
    }

    public struct RawAnimatedMaps
    {
        public readonly ushort[] Lods;
        public RawConditionalTable Conditionals;

        public RawAnimatedMaps(FArchiveBigEndian Ar)
        {
            Lods = Ar.ReadArray<ushort>();
            Conditionals = new RawConditionalTable(Ar);
        }
    }

    public struct RawBehavior
    {
        public readonly RawControls Controls;
        public readonly RawJoints Joints;
        public readonly RawBlendShapeChannels BlendShapeChannels;
        public readonly RawAnimatedMaps AnimatedMaps;

        public RawBehavior(FArchiveBigEndian Ar)
        {
            Controls = new RawControls(Ar);
            Joints = new RawJoints(Ar);
            BlendShapeChannels = new RawBlendShapeChannels(Ar);
            AnimatedMaps = new RawAnimatedMaps(Ar);
        }

        public RawBehavior(FArchiveBigEndian Ar, SectionLookupTable sections)
        {
            // TODO: Only seek based on condition of file version
            Ar.Seek(sections.OffsetStart + sections.Controls, System.IO.SeekOrigin.Begin);
            Controls = new RawControls(Ar);

            Ar.Seek(sections.OffsetStart + sections.Joints, System.IO.SeekOrigin.Begin);
            Joints = new RawJoints(Ar);

            Ar.Seek(sections.OffsetStart + sections.BlendShapeChannels, System.IO.SeekOrigin.Begin);
            BlendShapeChannels = new RawBlendShapeChannels(Ar);

            Ar.Seek(sections.OffsetStart + sections.AnimatedMaps, System.IO.SeekOrigin.Begin);
            AnimatedMaps = new RawAnimatedMaps(Ar);
        }
    }
}