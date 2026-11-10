param([switch]$Distribution)
. "$PSScriptRoot/Environment.ps1"
$taskUiVendor = Join-Path $ProjectRoot 'vendor/MinasisUI/1.0.10'
$taskUiOrigin = Get-Content -LiteralPath (Join-Path $taskUiVendor 'ADOPTION.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath (Join-Path $taskUiVendor 'MinasisUI.dll')).Hash -ne $taskUiOrigin.DllSHA256) { throw 'Pinned MinasisUI DLL mismatch' }
New-Item -ItemType Directory -Force 'artifacts/checks','artifacts/plugin' | Out-Null
foreach($taskProject in @('src/PluginChatOverlay.csproj','tests/Checks.csproj')) {
  # Explicitly select the mode; inherited MSBuild properties must not change Dev storage.
  $taskBuildOptions = @('-p:DistributionBuild=false')
  if ($Distribution -and $taskProject -eq 'src/PluginChatOverlay.csproj') {
    $taskBuildOptions = @(
      '-p:DistributionBuild=true'
      "-p:BaseOutputPath=$(Join-Path $ProjectRoot 'artifacts/distribution-build/bin/')"
      "-p:BaseIntermediateOutputPath=$(Join-Path $ProjectRoot '.work/distribution-build/obj/')"
    )
  }
  dotnet build $taskProject -c Release --disable-build-servers -p:UseSharedCompilation=false @taskBuildOptions
  if($LASTEXITCODE -ne 0) { throw "Build failed: $taskProject" }
}
$taskCheckArgs=@('.dependencies/Dalamud-15.0.3.6/Dalamud.dll')
if(Test-Path -LiteralPath 'research/masked/MaskedDalamud.dll') { $taskCheckArgs+='research/masked/MaskedDalamud.dll' }
dotnet 'artifacts/bin/Checks/Release/net10.0-windows/Checks.dll' @taskCheckArgs | Tee-Object 'artifacts/checks/results.txt'
if($LASTEXITCODE -ne 0) { throw 'Checks failed' }
if ($Distribution) {
  Write-Output "Distribution DLL: $(Join-Path $ProjectRoot 'artifacts/distribution-build/bin/Release/net10.0-windows/PluginChatOverlay.dll')"
  return
}
Get-ChildItem 'artifacts/bin/PluginChatOverlay/Release/net10.0-windows' -File | Where-Object Name -NE 'PluginChatOverlay.dll' | Copy-Item -Destination 'artifacts/plugin' -Force
Copy-Item -LiteralPath 'artifacts/bin/PluginChatOverlay/Release/net10.0-windows/PluginChatOverlay.dll' -Destination 'artifacts/plugin/PluginChatOverlay.dll' -Force
New-Item -ItemType Directory -Force 'artifacts/plugin/images' | Out-Null
Copy-Item -LiteralPath 'images/icon.png' -Destination 'artifacts/plugin/images/icon.png' -Force
Write-Output "Development DLL: $(Join-Path $ProjectRoot 'artifacts/plugin/PluginChatOverlay.dll')"
