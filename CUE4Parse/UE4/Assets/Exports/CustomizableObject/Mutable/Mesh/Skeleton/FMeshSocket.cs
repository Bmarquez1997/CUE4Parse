using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Math;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable.Mesh.Skeleton;

/// <summary>
/// Matches C++ FMeshSocket serialization: SocketName, BoneName, RelativeLocation, RelativeRotation, RelativeScale, bForceAlwaysAnimated.
/// Priority is not serialized in C++.
/// </summary>
public class FMeshSocket
{
    public string SocketName;
    public string BoneName;
    public FVector RelativeLocation;
    public FRotator RelativeRotation;
    public FVector RelativeScale;
    public bool bForceAlwaysAnimated;

    public FMeshSocket(FMutableArchive Ar)
    {
        SocketName = Ar.ReadMutableFString();
        BoneName = Ar.ReadMutableFString();
        RelativeLocation = Ar.Read<FVector>();
        RelativeRotation = Ar.Read<FRotator>();
        RelativeScale = Ar.Read<FVector>();
        bForceAlwaysAnimated = Ar.ReadFlag();
    }
}