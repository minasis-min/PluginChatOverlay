# Dependencies

PluginChatOverlay uses the libraries provided by the Dalamud host. Host DLLs are not included in Git or release archives. Obtain the matching SDK from your Dalamud installation; do not substitute a different build merely because its API level matches.

| Component | Pinned build / source | Integration |
| --- | --- | --- |
| .NET SDK | 10.0.302, latest patch within this feature band | Build tool; `global.json` |
| Dalamud | 15.0.3.6, API 15 | Host references in `.dependencies/Dalamud-15.0.3.6/` |
| FFXIVClientStructs | Source commit `313161e448e335adddd928f8a0212b2c328b5659` | Host-owned client bindings |
| MinasisUI | 0.1.8, source commit `25787082701ce9716afc506f90972e550cb308b0` | Three source files embedded in the plugin |

The supported Dalamud.dll SHA256 is `C726C1DE70B76A7ACC970F48E29589CD61205F08DAEC9586D112A977583055F5`, module ID `9a6f3e4f-c2f0-46f4-b211-7013dde20a8b`. The supported FFXIVClientStructs module ID is `7fda2a82-4ca8-49d0-9687-4c8eac011509`. The plugin checks these compatibility boundaries. Changing them requires implementation review and testing, not just editing a version number.

Build references: Dalamud, Dalamud.Bindings.ImGui, FFXIVClientStructs, InteropGenerator.Runtime, Lumina, Lumina.Excel, Newtonsoft.Json. Tests additionally use the host's Mono.Cecil and cimgui.dll. The SDK must include their runtime dependencies to execute checks.

MinasisUI's exact file hashes are recorded in `vendor/MinasisUI/0.1.8/origin.json` and verified before building. Its original LICENSE is retained. No independent MinasisUI DLL is required. No Demo, Preview or reference projects are distributed.

External component licenses and attribution are listed in THIRD-PARTY-NOTICES.txt. PluginChatOverlay's BSD license covers its original code and assets; it does not relicense the host or game resources.
