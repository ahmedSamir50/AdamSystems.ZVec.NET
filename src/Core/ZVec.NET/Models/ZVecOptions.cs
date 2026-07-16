namespace ZVec.NET;

/// <summary>
/// Process-wide configuration options for the ZVec library.
/// </summary>
public sealed class ZVecOptions
{
    /// <summary>The log type to use (Console or File).</summary>
    public ZVecLogType LogType { get; set; } = ZVecDefaults.GlobalOptions.LogType;

    /// <summary>The minimum log level to output.</summary>
    public ZVecLogLevel LogLevel { get; set; } = ZVecDefaults.GlobalOptions.LogLevel;

    /// <summary>The thread count for queries (-1 = auto).</summary>
    public int QueryThreads { get; set; } = ZVecDefaults.GlobalOptions.QueryThreads;

    /// <summary>Maximum concurrent native calls.</summary>
    public int MaxConcurrentNativeCalls { get; set; } = ZVecDefaults.GlobalOptions.MaxConcurrentNativeCalls;

    /// <summary>
    /// Process-wide memory limit (MB).
    /// </summary>
    public int? MemoryLimitMb { get; set; }

    /// <summary>
    /// Process-wide directory for Jieba dictionary files.
    /// </summary>
    public string? JiebaDictDir { get; set; }

    /// <summary>
    /// Directory for file logs. Required if LogType is File.
    /// </summary>
    public string? LogDir { get; set; }

    /// <summary>
    /// Base name of log files. Required if LogType is File.
    /// </summary>
    public string? LogBasename { get; set; }

    /// <summary>
    /// Log file size limit in MB.
    /// </summary>
    public uint LogFileSizeMb { get; set; } = 10;

    /// <summary>
    /// Number of days before log files expire.
    /// </summary>
    public uint LogOverdueDays { get; set; } = 7;
}
