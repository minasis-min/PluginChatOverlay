. "$PSScriptRoot/Environment.ps1"
$taskVendor = Join-Path $ProjectRoot 'vendor/MinasisUI/0.1.14'
$taskOrigin = Get-Content -LiteralPath (Join-Path $taskVendor 'origin.json') -Raw | ConvertFrom-Json -AsHashtable
foreach ($taskFile in $taskOrigin.Files.GetEnumerator()) {
    if ((Get-FileHash -LiteralPath (Join-Path $taskVendor $taskFile.Key) -Algorithm SHA256).Hash -ne $taskFile.Value) {
        throw "Pinned MinasisUI source changed: $($taskFile.Key)"
    }
}
foreach ($taskProject in @('src/PluginChatOverlay.csproj', 'tests/Checks.csproj')) {
    dotnet build $taskProject -c Release --disable-build-servers -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $taskProject" }
}
New-Item -ItemType Directory -Force -Path 'artifacts/checks' | Out-Null
dotnet 'artifacts/bin/Checks/Release/net10.0-windows/Checks.dll' '.dependencies/Dalamud-15.0.3.6/Dalamud.dll' |
    Tee-Object 'artifacts/checks/results.txt'
if ($LASTEXITCODE -ne 0) { throw 'Checks failed' }
