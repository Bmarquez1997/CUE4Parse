using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.Animation.CurveExpression;

public class FExpressionObject
{
    public List<OpElement> Expression = [];
    
    public FExpressionObject(FArchive Ar)
    {
        var operandCount = Ar.Read<int>();
        for (var operandIndex = 0; operandIndex < operandCount; operandIndex++)
        {
            var operandType = Ar.Read<int>();
            switch (operandType)
            {
                case OpElement.EOperator: 
                {
                    var operatorType = Ar.Read<int>();
                    Expression.Add(new OpElement<EOperator>((EOperator) operatorType));
                    break;
                }
                case OpElement.FName:
                {
                    Expression.Add(new OpElement<FName>(Ar.ReadFName()));
                    break;
                }
                case OpElement.FFunctionRef:
                {
                    var functionIndex = Ar.Read<int>();
                    Expression.Add(new OpElement<FFunctionRef>(new FFunctionRef(functionIndex)));
                    break;
                }
                case OpElement.Float:
                {
                    var value = Ar.Read<float>();
                    Expression.Add(new OpElement<float>(value));
                    break;
                }
                default:
                {
                    throw new ParserException($"Invalid operand type: {operandType}");
                }
            }
        }
    }
    
    public override string ToString()
    {
        List<string> items = new List<string>();
        foreach (var element in Expression)
        {
            if (element.TryGet<EOperator>(out var op))
            {
                switch (op)
                {
                    case EOperator.Negate:
                        items.Add("Op[Negate]");
                        break;
                    case EOperator.Add:
                        items.Add("Op[Add]");
                        break;
                    case EOperator.Subtract:
                        items.Add("Op[Subtract]");
                        break;
                    case EOperator.Multiply:
                        items.Add("Op[Multiply]");
                        break;
                    case EOperator.Divide:
                        items.Add("Op[Divide]");
                        break;
                    case EOperator.Modulo:
                        items.Add("Op[Modulo]");
                        break;
                    case EOperator.Power:
                        items.Add("Op[Power]");
                        break;
                    case EOperator.FloorDivide:
                        items.Add("Op[FloorDivide]");
                        break;
                }
            }
            else if (element.TryGet<FName>(out var constant))
            {
                items.Add($"C[{constant}]");
            }
            else if (element.TryGet<FFunctionRef>(out var funcRef))
            {
                items.Add($"F[{funcRef.Index}]");
            }
            else if (element.TryGet<float>(out var value))
            {
                items.Add($"V[{value}]");
            }
            else
            {
                throw new NotImplementedException($"{element}: unimplemented expression operation");
            }
        }
        return string.Join(" ", items);
    }
}

[JsonConverter(typeof(EOperatorConverter))]
public enum EOperator : int
{
    Negate,				// Negation operator.
    Add,				// Add last two values on stack and add the result to the stack.
    Subtract,			// Same but subtract 
    Multiply,			// Same but multiply
    Divide,				// Same but divide (div-by-zero returns zero)
    Modulo,				// Same but modulo (mod-by-zero returns zero)
    Power,				// Raise to power
    FloorDivide,    	// Divide and round the result down
}

[JsonConverter(typeof(FFunctionRefConverter))]
public struct FFunctionRef
{
    public int Index;
    
    public FFunctionRef(int index)
    {
        Index = index;
    }
}

public class OpElement
{
    public const int EOperator = 0;
    public const int FName = 1;
    public const int FFunctionRef = 2;
    public const int Float = 3;

    public bool TryGet<T>(out T outValue)
    {
        if (this is OpElement<T> op)
        {
            outValue = op.Value;
            return true;
        }

        outValue = default;
        return false;
    }
}

public class OpElement<T> : OpElement
{
    public T Value;

    public OpElement(T value)
    {
        Value = value;
    }
}