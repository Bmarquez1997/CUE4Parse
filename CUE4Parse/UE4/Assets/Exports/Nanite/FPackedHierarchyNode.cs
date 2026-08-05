using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using static CUE4Parse.UE4.Assets.Exports.Nanite.NaniteConstants;

namespace CUE4Parse.UE4.Assets.Exports.Nanite;

public class FPackedHierarchyNode
{
    public FHierarchyNodeSlice[] Slices;

    public FPackedHierarchyNode(FArchive Ar)
    {
        // Early Nanite (Fortnite S20 / pre-fanout change) serialized 8 children per node; later builds use 4.
        var fanout = Ar.Game == GAME_Fortnite_S20 ? 1 << 3 : NANITE_MAX_BVH_NODE_FANOUT;
        Slices = Ar.ReadArray(fanout, () => new FHierarchyNodeSlice(Ar));
    }
}
