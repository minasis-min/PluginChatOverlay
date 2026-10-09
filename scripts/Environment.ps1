$ErrorActionPreference='Stop'
$script:ProjectRoot=Split-Path $PSScriptRoot -Parent
$taskLocations=@{
  DOTNET_CLI_HOME='.work/cli'; NUGET_PACKAGES='.work/nuget/packages'
  NUGET_HTTP_CACHE_PATH='.work/nuget/http'; NUGET_PLUGINS_CACHE_PATH='.work/nuget/plugins'
  NUGET_SCRATCH='.work/nuget/scratch'; TEMP='.work/temp'; TMP='.work/temp'
  APPDATA='.work/appdata'; LOCALAPPDATA='.work/localappdata'
  DOTNET_BUNDLE_EXTRACT_BASE_DIR='.work/bundles'; XDG_CACHE_HOME='.work/cache'
}
foreach($taskEntry in $taskLocations.GetEnumerator()) {
  $taskPath=Join-Path $ProjectRoot $taskEntry.Value
  New-Item -ItemType Directory -Force $taskPath | Out-Null
  [Environment]::SetEnvironmentVariable($taskEntry.Key,$taskPath,'Process')
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_NOLOGO='1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='true'
$env:MSBUILDDISABLENODEREUSE='1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
Set-Location -LiteralPath $ProjectRoot
