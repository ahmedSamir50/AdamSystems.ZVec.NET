# Edge and offline devices

ZVec is designed for local and edge deployments. MAUI Blazor Hybrid samples store collections under the app AppData directory with EnableMmap set to true.

Native binaries are RID-specific. Until NuGet multi-RID packaging (Epic E21), copy zvec_c_api for your current RID into the app output as documented in Appendix C.
