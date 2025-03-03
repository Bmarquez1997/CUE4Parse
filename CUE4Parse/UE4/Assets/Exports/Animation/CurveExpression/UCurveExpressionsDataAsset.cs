using System.Collections.Generic;
using CommunityToolkit.HighPerformance;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.Animation.CurveExpression;

public class UCurveExpressionsDataAsset : UObject
{
    public FName[] NamedConstants;
    public FExpressionData ExpressionData;
    
    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);
        
        if (FCurveExpressionCustomVersion.Get(Ar) >= FCurveExpressionCustomVersion.Type.ExpressionDataInSharedObject)
        {
            NamedConstants = Ar.ReadArray(Ar.ReadFName);
        }

        ExpressionData = new FExpressionData(Ar);
    }
    
    protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
    {
        base.WriteJson(writer, serializer);

        writer.WritePropertyName(nameof(NamedConstants));
        writer.WriteStartArray();
        foreach (var name in NamedConstants)
            serializer.Serialize(writer, name);
        writer.WriteEndArray();


        writer.WritePropertyName(nameof(ExpressionData));
        writer.WriteStartObject();
        foreach (var (name, expression) in ExpressionData.ExpressionMap)
        {
            writer.WritePropertyName(name.Text);
            writer.WriteStartObject();
            writer.WritePropertyName("Expanded");
            serializer.Serialize(writer, expression);
            writer.WritePropertyName("Concise");
            serializer.Serialize(writer, expression.ToString());
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }
}

public class FExpressionData
{
    public Dictionary<FName, FExpressionObject> ExpressionMap;
    
    public FExpressionData(FArchive Ar)
    {
        ExpressionMap = Ar.ReadMap(() => (Ar.ReadFName(), new FExpressionObject(Ar)));
    }
}