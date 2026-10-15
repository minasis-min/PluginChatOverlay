# Dependencies

PluginChatOverlay uses the libraries provided by the Dalamud host. Host DLLs are not included in Git or release archives. Obtain the matching SDK from your Dalamud installation; do not substitute a different build merely because its API level matches.

| Component | Pinned build / source | Integration |
| --- | --- | --- |
| .NET SDK | 10.0.302, latest patch within this feature band | Build tool; `global.json` |
| Dalamud | 15.0.3.6, API 15 | Host references in `.dependencies/Dalamud-15.0.3.6/` |
| FFXIVClientStructs | Source commit `313161e448e335adddd928f8a0212b2c328b5659` | Host-owned client bindings |
| MinasisUI | 0.1.14, local source snapshot verified 2026-10-12 (SHA256 in origin.json) | Four source files embedded in the plugin |

The pinned DLLs above are build references. Runtime compatibility is checked per feature: notification routing checks the required chat methods, native tooltips check the SDK fields and APIs they use, and text selection checks the renderer schema. Chat expansion hiding checks the SDK field offsets and the inspected game function fingerprint; only the displacement of an unused internal relative CALL is excluded. The game function is never invoked. An incompatible feature is disabled, and notification routing falls back to the normal game chat when its check fails.

Build references: Dalamud, Dalamud.Bindings.ImGui, FFXIVClientStructs, InteropGenerator.Runtime, Lumina, Lumina.Excel, Newtonsoft.Json. Tests additionally use the host's Mono.Cecil and cimgui.dll. The SDK must include their runtime dependencies to execute checks.

MinasisUI's exact file hashes are recorded in `vendor/MinasisUI/0.1.14/origin.json` and verified before building. Its copyright notices are retained; this project copy is licensed under MIT by its copyright holder. No independent MinasisUI DLL is required. No Demo, Preview or reference projects are distributed.

External component licenses and attribution are listed below. PluginChatOverlay's MIT license covers its original code and assets; it does not relicense the host or game resources.

## ビルド・配布ZIPの作成

PowerShell 7と上記の.NET SDKを用意し、対応するDalamud SDKのファイルを `.dependencies/Dalamud-15.0.3.6/` に配置します。リポジトリのルートで実行してください。

```powershell
pwsh -NoProfile -File ./scripts/build.ps1
pwsh -NoProfile -File ./scripts/package.ps1
```

公開用ソースでは配布モードが既定です。成果物は `artifacts/`、一時ファイルは `.work/` に生成し、Gitには登録しません。ZIPには本体DLL、manifest、deps.json、アイコン、LICENSEを含めます。

ゲーム・Dalamud更新時の互換性は機能別に判定します。配布版の設定はDalamud標準のプラグイン用設定フォルダへ保存され、Dドライブは必要ありません。

## External component licenses

External Components Provided by Dalamud
--------------------------------------
The following components are provided by the Dalamud host and are not
bundled with this plugin. The MIT license for PluginChatOverlay
does not replace or override their licenses.

Dalamud
License: AGPL-3.0
https://github.com/goatcorp/Dalamud/blob/b666d821a47306fb447c60155b5d99377f91a5ee/LICENSE

FFXIVClientStructs
Copyright (c) 2021-2023 aers.
License: MIT
https://github.com/aers/FFXIVClientStructs/blob/313161e448e335adddd928f8a0212b2c328b5659/LICENSE

Lumina
License: WTFPL-2.0
https://github.com/NotAdam/Lumina/blob/master/LICENSE

Newtonsoft.Json
Copyright (c) 2007 James Newton-King.
License: MIT
https://github.com/JamesNK/Newtonsoft.Json/blob/master/LICENSE.md

Dear ImGui
Copyright (c) 2014-2026 Omar Cornut.
License: MIT
https://github.com/ocornut/imgui/blob/master/LICENSE.txt

Dalamud also provides the API bindings, native libraries,
InteropGenerator.Runtime and icon fonts used by this plugin. These are
not bundled with this plugin; their original licenses and notices remain
applicable.

ゲーム内の併用確認項目は [TESTING.md](TESTING.md) を参照してください。
