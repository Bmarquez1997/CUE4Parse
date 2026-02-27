using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Engine.Curves;

namespace CUE4Parse.UE4.Assets.Exports.Texture;

public class UCurveLinearColorAtlas : UTexture2D
{
    public int TextureSize;
    public UCurveLinearColor[] GradientCurves;

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);
        
        TextureSize = GetOrDefault<int>(nameof(TextureSize));
        GradientCurves = GetOrDefault<UCurveLinearColor[]>(nameof(GradientCurves));
    }
}
