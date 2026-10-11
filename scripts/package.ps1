. "$PSScriptRoot/Environment.ps1"
& "$PSScriptRoot/build.ps1"

$taskBinary = Join-Path $ProjectRoot 'artifacts/bin/PluginChatOverlay/Release/net10.0-windows'
$taskDll = Join-Path $taskBinary 'PluginChatOverlay.dll'
$taskVersion = [Reflection.AssemblyName]::GetAssemblyName($taskDll).Version.ToString()
$taskManifest = Get-Content -LiteralPath 'src/PluginChatOverlay.json' -Raw | ConvertFrom-Json -AsHashtable
if ($taskManifest.AssemblyVersion -ne $taskVersion) { throw 'Manifest/DLL version mismatch' }
if ($taskManifest.RepoUrl -ne 'https://github.com/minasis-min/PluginChatOverlay') { throw 'Unexpected source repository' }
$taskTag = 'v' + $taskVersion
$taskReleaseBase = "https://github.com/minasis-min/PluginChatOverlay/releases/download/$taskTag"
if ($taskManifest.IconUrl -ne "$taskReleaseBase/icon.png") { throw 'Icon URL version mismatch' }

[Reflection.Assembly]::LoadFrom((Join-Path $ProjectRoot '.dependencies/Dalamud-15.0.3.6/Mono.Cecil.dll')) | Out-Null
$taskModule = [Mono.Cecil.ModuleDefinition]::ReadModule($taskDll)
try {
    $taskConstructor = $taskModule.Types.Where({ $_.FullName -eq 'PluginChatOverlay.Plugin' }).Methods.Where({ $_.Name -eq '.ctor' })
    $taskStorage = @($taskConstructor.Body.Instructions.Where({ $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'PluginChatOverlay.SettingsStore' }))
    if (!$taskStorage.Where({ $_.Operand.Name -eq 'ForDistribution' }).Count -or $taskStorage.Where({ $_.Operand.Name -eq '.ctor' }).Count) {
        throw 'Distribution must use Dalamud ConfigDirectory'
    }
    if ($taskModule.HasSymbols) { throw 'Distribution must not contain debug symbols' }
    if (!$taskModule.Types.Where({ $_.FullName -eq 'MinasisUI.Ui' }).Count -or $taskModule.AssemblyReferences.Where({ $_.Name -eq 'MinasisUI' }).Count) {
        throw 'MinasisUI must be source embedded'
    }
}
finally { $taskModule.Dispose() }
$taskPeStream = [IO.File]::OpenRead($taskDll)
$taskPe = [System.Reflection.PortableExecutable.PEReader]::new($taskPeStream)
try {
    if (@($taskPe.ReadDebugDirectory() | Where-Object { $_.Type -in @('CodeView', 'EmbeddedPortablePdb') }).Count) {
        throw 'Distribution contains a PDB or build path'
    }
}
finally { $taskPe.Dispose(); $taskPeStream.Dispose() }

$taskDeps = Get-Content -LiteralPath (Join-Path $taskBinary 'PluginChatOverlay.deps.json') -Raw | ConvertFrom-Json -AsHashtable
if (@($taskDeps.libraries.Keys).Count -ne 1 -or !($taskDeps.libraries.ContainsKey("PluginChatOverlay/$taskVersion"))) {
    throw 'Unexpected dependency metadata'
}
$taskIcon = [IO.File]::ReadAllBytes((Join-Path $ProjectRoot 'images/icon.png'))
if ([Convert]::ToHexString($taskIcon[0..7]) -ne '89504E470D0A1A0A' -or [Convert]::ToHexString($taskIcon[16..23]) -ne '0000020000000200') {
    throw 'Icon must be a 512x512 PNG'
}

$taskDirectory = Join-Path $ProjectRoot "artifacts/distribution/$taskVersion"
New-Item -ItemType Directory -Force -Path $taskDirectory | Out-Null
$taskStage = Join-Path $ProjectRoot ('.work/package/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskStage | Out-Null
$taskAllowed = [ordered]@{
    'PluginChatOverlay.dll' = $taskDll
    'PluginChatOverlay.json' = Join-Path $ProjectRoot 'src/PluginChatOverlay.json'
    'PluginChatOverlay.deps.json' = Join-Path $taskBinary 'PluginChatOverlay.deps.json'
    'images/icon.png' = Join-Path $ProjectRoot 'images/icon.png'
    'LICENSE' = Join-Path $ProjectRoot 'LICENSE'
}
$taskZip = Join-Path $taskDirectory 'PluginChatOverlay.zip'
if (Test-Path -LiteralPath $taskZip) { throw 'Existing package retained; use a new version or a clean output directory' }
$taskArchive = [IO.Compression.ZipFile]::Open($taskZip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($taskFile in $taskAllowed.GetEnumerator()) {
        $taskTarget = Join-Path $taskStage $taskFile.Key
        New-Item -ItemType Directory -Force -Path (Split-Path $taskTarget -Parent) | Out-Null
        Copy-Item -LiteralPath $taskFile.Value -Destination $taskTarget
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskArchive, $taskTarget, $taskFile.Key, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $taskArchive.Dispose() }
$taskArchive = [IO.Compression.ZipFile]::OpenRead($taskZip)
try {
    if (@(Compare-Object @($taskArchive.Entries.FullName | Sort-Object) @($taskAllowed.Keys | Sort-Object)).Count) { throw 'Unexpected ZIP contents' }
    foreach ($taskFile in $taskAllowed.GetEnumerator()) {
        $taskStream = $taskArchive.GetEntry($taskFile.Key).Open()
        try { $taskHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskStream)) }
        finally { $taskStream.Dispose() }
        if ($taskHash -ne (Get-FileHash -LiteralPath $taskFile.Value -Algorithm SHA256).Hash) { throw "ZIP mismatch: $($taskFile.Key)" }
    }
}
finally { $taskArchive.Dispose() }
Copy-Item -LiteralPath (Join-Path $ProjectRoot 'images/icon.png') -Destination (Join-Path $taskDirectory 'icon.png')
$taskHash = (Get-FileHash -LiteralPath $taskZip -Algorithm SHA256).Hash.ToLowerInvariant()
"$taskHash  PluginChatOverlay.zip" | Set-Content -LiteralPath (Join-Path $taskDirectory 'SHA256SUMS.txt') -Encoding utf8NoBOM
$taskEntry = $taskManifest.Clone()
$taskEntry.IsHide = $false
$taskEntry.IsTestingExclusive = $false
$taskEntry.CategoryTags = @('UI')
$taskEntry.DownloadLinkInstall = "$taskReleaseBase/PluginChatOverlay.zip"
$taskEntry.DownloadLinkUpdate = "$taskReleaseBase/PluginChatOverlay.zip"
$taskEntry.LastUpdate = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$taskEntry.Changelog = '1.0.5: レイアウト設定を拡充・整理。スクロールバー、背景の縁、文字の縁取り・間隔、時刻形式を調整可能。標準チャット拡大中の非表示とボタン表示を改善。'
$taskEntry | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath (Join-Path $taskDirectory 'repo-entry.json') -Encoding utf8NoBOM
Write-Output "Verified release package: $taskZip"
Write-Output "ZIP SHA256: $taskHash"
