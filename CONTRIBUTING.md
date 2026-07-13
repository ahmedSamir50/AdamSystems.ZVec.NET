# Contributing to AdamSystems.ZVec.NET

First off, thank you for considering contributing to AdamSystems.ZVec.NET! Our goal is to bring the blazing fast performance of Alibaba's ZVec to the .NET ecosystem, and community support makes that possible.

## Repository Architecture

This repository contains two main components:
1. **ZVec.Core (C#):** The managed .NET SDK exposing an idiomatic, zero-allocation API.
2. **ZVec.Native (C++):** A thin `extern "C"` bridge connecting the .NET environment to Alibaba's core C++ engine.

## Local Development Setup

Because this project relies on a native C++ engine, you cannot simply press "Run" in Visual Studio immediately. You must initialize the C++ submodules first.

1. **Clone the repository with submodules:**
   If you already cloned the repo normally, run:
   `git submodule update --init --recursive`
   *(This downloads the upstream Alibaba C++ code into `src/ZVec.Native/external/zvec`).*

2. **C++ Compilation:**
   Ensure you have CMake and a C++ compiler (Visual Studio 2026 with C++ Desktop Development workload, or GCC/Clang on Linux/macOS) installed. Visual Studio should automatically detect the `CMakeLists.txt` file in `src/ZVec.Native`.

## How to Contribute

1. **Branching:** Never work directly on `main`. Create a feature branch off the `dev` branch (e.g., `feature/add-hybrid-search`).
2. **Pull Requests:** Submit all Pull Requests against the `dev` branch.
3. **Zero Allocation:** When modifying the hot paths (like `Query` or `Insert`), you must use `ReadOnlySpan<float>` and `MemoryHandle`. Do not introduce new heap allocations (`new float[]`) on the vector passing paths.
4. **Testing:** Run the `ZVec.Core.Tests` project. We use a mock native library for unit testing to ensure tests run fast without requiring full native recompilation on every change.

If you are unsure where to start, check the Issues tab for "good first issue" tags!