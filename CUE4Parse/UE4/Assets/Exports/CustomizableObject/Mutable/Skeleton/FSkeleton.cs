using CUE4Parse.UE4.Readers;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Skeleton;

public class FSkeleton
{
    public FBoneName[] BoneIds;
    public short[] BoneParents;

    public FSkeleton(FArchive Ar)
    {
        BoneIds = Ar.ReadArray(() => new FBoneName(Ar));
        BoneParents = Ar.ReadArray<short>();
    }
}
