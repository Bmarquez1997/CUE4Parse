using System.Collections.Concurrent;
using System.Reflection;
using CUE4Parse.UE4.Assets.Objects;

namespace CUE4Parse.UE4.Assets.Exports;

public class UPropertyAttribute(string? propertyName = null) : Attribute
{
    public readonly string? PropertyName = propertyName;
}

internal record UPropertyEntry(FieldInfo Field, string Name);

internal static class UPropertyCache
{
    private static readonly ConcurrentDictionary<Type, UPropertyEntry[]> Cache = new();

    public static void ApplyProperties(IPropertyHolder self)
    {
        ApplyProperties(self, self);
    }

    public static void ApplyProperties(IPropertyHolder src, object dst)
    {
        var cached = GetPropertiesByType(dst.GetType());
        if (cached.Length == 0) return;

        var propMap = new Dictionary<string, FPropertyTag>(src.Properties.Count);
        foreach (var tag in src.Properties)
        {
            // fixed size arrays serialize one tag per element under the same name, keep index 0
            propMap.TryAdd(tag.Name.Text, tag);
        }

        foreach (var (field, name) in cached)
        {
            if (!propMap.TryGetValue(name, out var property))
                continue;

            field.SetValue(dst, property.Tag?.GetValue(field.FieldType));
        }
    }

    private static UPropertyEntry[] GetPropertiesByType(Type type) =>
        Cache.GetOrAdd(type, static t =>
            t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Select(f => (Field: f, Attr: f.GetCustomAttribute<UPropertyAttribute>()))
                .Where(x => x.Attr is not null)
                .Select(x => new UPropertyEntry(x.Field, Name: x.Attr!.PropertyName ?? x.Field.Name))
                .ToArray()
        );
}
