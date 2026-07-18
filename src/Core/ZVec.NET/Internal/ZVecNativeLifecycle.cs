using ZVec.NET.Exceptions;
using ZVec.NET.Interop;

namespace ZVec.NET.Internal;

/// <summary>
/// Process-wide native library lifecycle: load, ABI gate, <c>zvec_initialize</c> / <c>zvec_shutdown</c>, refcount.
/// Thread-safe via a single global lock. Not a public API — <see cref="ZVecFactory"/> remains the consumer entry point.
/// </summary>
internal static class ZVecNativeLifecycle
{
    private static int _globalNativeInitCount;
    private static readonly object GlobalInitLock = new();

    /// <summary>True when at least one factory holds a live native init reference.</summary>
    internal static bool IsNativeLibraryInitialized
    {
        get
        {
            lock (GlobalInitLock) return _globalNativeInitCount > 0;
        }
    }

    /// <summary>
    /// Ensures the native library is loaded and initialized (first caller applies <paramref name="options"/>).
    /// Increments the process-wide refcount. Must be paired with <see cref="Release"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if this call performed <c>zvec_initialize</c> (caller owns rollback on later failure
    /// before <see cref="Release"/>); otherwise <see langword="false"/>.
    /// </returns>
    internal static bool Acquire(ZVecOptions? options)
    {
        lock (GlobalInitLock)
        {
            var nativeInitOwned = false;
            try
            {
                if (_globalNativeInitCount == 0)
                {
                    NativeLibraryResolver.EnsureLoaded();
                    ApplyNativeConfig(options);
                    nativeInitOwned = true;
                }
                else
                {
                    NativeLibraryResolver.EnsureLoaded();
                }

                CheckAbiVersion();
                _globalNativeInitCount++;
                return nativeInitOwned;
            }
            catch
            {
                if (nativeInitOwned)
                {
                    try { NativeMethods.zvec_shutdown(); }
                    catch { /* best-effort rollback */ }
                }

                throw;
            }
        }
    }

    /// <summary>
    /// Decrements the process-wide refcount and calls <c>zvec_shutdown</c> when it reaches zero.
    /// </summary>
    internal static void Release()
    {
        lock (GlobalInitLock)
        {
            _globalNativeInitCount--;
            if (_globalNativeInitCount != 0)
                return;

            try
            {
                NativeMethods.zvec_shutdown();
            }
            catch (DllNotFoundException)
            {
                // Native library already unloaded or resolver poisoned.
            }
        }
    }

    /// <summary>Runs <paramref name="action"/> while holding the global init lock (factory state transitions).</summary>
    internal static void WithGlobalLock(Action action)
    {
        lock (GlobalInitLock)
        {
            action();
        }
    }

    /// <summary>Runs <paramref name="func"/> while holding the global init lock.</summary>
    internal static T WithGlobalLock<T>(Func<T> func)
    {
        lock (GlobalInitLock)
        {
            return func();
        }
    }

    private static void CheckAbiVersion()
    {
        if (ZVecDefaults.Version.BypassAbiCheck)
            return;

        int requiredMajor = ZVecDefaults.Version.ExpectedMajor;
        int requiredMinor = ZVecDefaults.Version.ExpectedMinor;
        int requiredPatch = ZVecDefaults.Version.ExpectedPatch;

        bool meetsMinimum = NativeMethods.zvec_check_version(requiredMajor, requiredMinor, requiredPatch);
        int foundMajor = NativeMethods.zvec_get_version_major();

        if (!ZVecNativeAbi.IsCompatible(meetsMinimum, foundMajor, requiredMajor))
        {
            string found = NativeMethods.GetVersionString();
            string minimum = $"{requiredMajor}.{requiredMinor}.{requiredPatch}";
            throw new ZVecAbiMismatchException(minimum, requiredMajor, found);
        }
    }

    private static void ApplyNativeConfig(ZVecOptions? options)
    {
        if (options is null)
        {
            ZVecError.ThrowIfFailed((ZVecErrorCode)NativeMethods.zvec_initialize(IntPtr.Zero), nameof(ZVecFactory.Initialize));
            return;
        }

        IntPtr cfg = NativeMethods.zvec_config_data_create();
        try
        {
            if (options.QueryThreads > 0)
                NativeMethods.zvec_config_data_set_query_thread_count(cfg, (uint)options.QueryThreads);

            if (options.MemoryLimitMb.HasValue)
                NativeMethods.zvec_config_data_set_memory_limit(cfg, (ulong)options.MemoryLimitMb.Value * 1024 * 1024);

            IntPtr logCfg = options.LogType == ZVecLogType.File
                ? NativeMethods.zvec_config_log_create_file(
                    (int)options.LogLevel,
                    options.LogDir ?? "./logs",
                    options.LogBasename ?? "zvec.log",
                    options.LogFileSizeMb,
                    options.LogOverdueDays)
                : NativeMethods.zvec_config_log_create_console((int)options.LogLevel);

            try
            {
                NativeMethods.zvec_config_data_set_log_config(cfg, logCfg);
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_initialize(cfg),
                    nameof(ZVecFactory.Initialize));
            }
            finally
            {
                NativeMethods.zvec_config_log_destroy(logCfg);
            }
        }
        finally
        {
            NativeMethods.zvec_config_data_destroy(cfg);
        }
    }
}
