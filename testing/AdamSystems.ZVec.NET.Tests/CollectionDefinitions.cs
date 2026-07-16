namespace AdamSystems.ZVec.NET.Tests;

/// <summary>
/// Serializes tests that mutate <c>NativeLibraryResolver</c> global state
/// so they never run concurrently with tests that depend on a clean resolver.
/// <c>DisableParallelization = true</c> prevents these tests from running
/// in parallel with tests in ANY other collection.
/// </summary>
[CollectionDefinition("ResolverStateTests", DisableParallelization = true)]
public class ResolverStateTestsCollectionDefinition;
