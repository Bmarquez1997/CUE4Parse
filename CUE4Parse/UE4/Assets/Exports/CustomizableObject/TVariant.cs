using System;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject;

public class TVariant<T1, T2>
{
    private byte TypeIndex;
    private object Value;

    // Constructor for T1
    public TVariant(T1 value)
    {
        Value = value;
        TypeIndex = 0;
    }

    // Constructor for T2
    public TVariant(T2 value)
    {
        Value = value;
        TypeIndex = 1;
    }

    public bool Is<T>() =>
        (typeof(T) == typeof(T1) && TypeIndex == 0) ||
        (typeof(T) == typeof(T2) && TypeIndex == 1);

    public T Get<T>()
    {
        if (!Is<T>())
            throw new InvalidOperationException($"TVariant does not hold type {typeof(T)}");
        return (T)Value;
    }
}