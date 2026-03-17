using System;
using System.IO;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;

/// <summary>
/// Optional file logging for parameter-to-resource resolution. Set LogPath before calling the resolver to write a parseable debug log.
/// </summary>
public static class MutableResolverDebugLog
{
    private static string? _logPath;
    private static StreamWriter? _writer;

    /// <summary> When set, resolver and bytecode will append lines to this file. Set to null to disable. </summary>
    public static string? LogPath
    {
        get => _logPath;
        set
        {
            _writer?.Dispose();
            _writer = null;
            _logPath = value;
        }
    }

    /// <summary> Clear the log file so this run starts fresh. Call after setting LogPath. </summary>
    public static void Reset()
    {
        if (string.IsNullOrEmpty(_logPath)) return;
        try { File.WriteAllText(_logPath, ""); } catch { /* ignore */ }
    }

    /// <summary> Write a line to the log file (no newline added). </summary>
    public static void Log(string message)
    {
        if (string.IsNullOrEmpty(_logPath)) return;
        try
        {
            _writer ??= new StreamWriter(_logPath, append: true) { AutoFlush = true };
            _writer.WriteLine(message);
        }
        catch { /* ignore */ }
    }

    /// <summary> Write a line with a numeric prefix for easy parsing (e.g. "DEPTH\t3\tIN_ADDLOD\t..."). </summary>
    public static void LogDepth(int depth, string message)
    {
        if (string.IsNullOrEmpty(_logPath)) return;
        Log($"DEPTH\t{depth}\t{message}");
    }
}
