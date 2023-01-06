using System;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.RenderCore;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.Animation
{
    public class FMorphTargetDelta
    {
        public readonly FVector PositionDelta;
        public readonly FVector TangentZDelta;
        public readonly uint SourceIdx;

        public FMorphTargetDelta(FArchive Ar)
        {
            PositionDelta = Ar.Read<FVector>();
            if (Ar.Ver < EUnrealEngineObjectUE4Version.MORPHTARGET_CPU_TANGENTZDELTA_FORMATCHANGE)
            {
                TangentZDelta = (FVector) Ar.Read<FDeprecatedSerializedPackedNormal>();
            }
            else
            {
                TangentZDelta = Ar.Read<FVector>();
            }
            SourceIdx = Ar.Read<uint>();
        }
    }

    public class FMorphTargetLODModel
    {
        /** vertex data for a single LOD morph mesh */
        public readonly FMorphTargetDelta[] Vertices;
        /** number of original verts in the base mesh */
        public readonly int NumBaseMeshVerts;
        /** list of sections this morph is used */
        public readonly int[] SectionIndices;
        /** Is this LOD generated by reduction setting */
        public readonly bool bGeneratedByEngine;

        public FMorphTargetLODModel(FArchive Ar)
        {
            if (FEditorObjectVersion.Get(Ar) < FEditorObjectVersion.Type.AddedMorphTargetSectionIndices)
            {
                Vertices = Ar.ReadArray(() => new FMorphTargetDelta(Ar));
                NumBaseMeshVerts = Ar.Read<int>();
                bGeneratedByEngine = false;
            }
            else if (FFortniteMainBranchObjectVersion.Get(Ar) < FFortniteMainBranchObjectVersion.Type.SaveGeneratedMorphTargetByEngine)
            {
                Vertices = Ar.ReadArray(() => new FMorphTargetDelta(Ar));
                NumBaseMeshVerts = Ar.Read<int>();
                SectionIndices = Ar.ReadArray<int>();
                bGeneratedByEngine = false;
            }
            else
            {
                var bVerticesAreStrippedForCookedBuilds = false;
                if (FUE5PrivateFrostyStreamObjectVersion.Get(Ar) >= FUE5PrivateFrostyStreamObjectVersion.Type.StripMorphTargetSourceDataForCookedBuilds)
                {
                    // Strip source morph data for cooked build if targets don't include mobile. Mobile uses CPU morphing which needs the source morph data.
                    bVerticesAreStrippedForCookedBuilds = Ar.ReadBoolean();
                }

                if (bVerticesAreStrippedForCookedBuilds)
                {
                    Ar.Position += 4; // NumVertices
                    Vertices = Array.Empty<FMorphTargetDelta>();
                }
                else
                {
                    Vertices = Ar.ReadArray(() => new FMorphTargetDelta(Ar));
                }

                NumBaseMeshVerts = Ar.Read<int>();
                SectionIndices = Ar.ReadArray<int>();
                bGeneratedByEngine = Ar.ReadBoolean();
            }
        }
    }

    public class UMorphTarget : UObject
    {
        public FMorphTargetLODModel[]? MorphLODModels;

        public override void Deserialize(FAssetArchive Ar, long validPos)
        {
            base.Deserialize(Ar, validPos);
            var stripData = Ar.Read<FStripDataFlags>();
            if (!stripData.IsDataStrippedForServer())
            {
                MorphLODModels = Ar.ReadArray(() => new FMorphTargetLODModel(Ar));
            }
        }

        protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
        {
            base.WriteJson(writer, serializer);

            writer.WritePropertyName(nameof(MorphLODModels));
            serializer.Serialize(writer, MorphLODModels);
        }
    }
}