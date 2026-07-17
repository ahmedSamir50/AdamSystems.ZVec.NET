# Native runtime assets

CI and local scripts deploy `zvec_c_api` shared libraries here:

```text
runtimes/{rid}/native/zvec_c_api.dll          # Windows
runtimes/{rid}/native/libzvec_c_api.so        # Linux / Android
runtimes/{rid}/native/libzvec_c_api.dylib     # macOS / iOS / Mac Catalyst
```

See `build/ci/deploy-native.sh` / `.ps1` and GitHub Actions workflows.
Binaries are produced by CI; do not commit large multi-RID binaries unless intentionally vendoring a single local RID for development.
