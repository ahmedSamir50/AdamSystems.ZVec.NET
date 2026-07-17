namespace ZVec.NET.Tests.Integration;

/// <summary>Serializes integration tests that mutate process-wide native/version state.</summary>
[CollectionDefinition(nameof(NativeSessionCollection), DisableParallelization = true)]
public sealed class NativeSessionCollection;
