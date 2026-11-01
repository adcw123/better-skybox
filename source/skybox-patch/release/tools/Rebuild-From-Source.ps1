param([Parameter(Mandatory=$true)][string]$GameDirectory,[string]$OriginalAssembly)
$ErrorActionPreference='Stop'
$package=Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'ReleaseCore.ps1')
$release=Assert-SkyboxPayload $package
$game=Resolve-SkyboxDirectory $GameDirectory
$source=Join-Path $package 'source\skybox-patch'
$found=$null
foreach($candidate in @($OriginalAssembly,(Join-Path $game 'SkyboxUsability.Backup\Assembly-CSharp.original.dll'),(Join-Path $game 'SteamVR_SourceVRPlayer_Data\Managed\Assembly-CSharp.dll'))) {
    if($candidate-and(Test-Path -LiteralPath $candidate)-and(Get-SkyboxHash $candidate)-eq$release.OriginalAssemblyHash) {$found=$candidate;break}
}
if(!$found) {throw 'A verified original Assembly-CSharp.dll is required to rebuild. Supply -OriginalAssembly or restore the original using Steam first.'}
New-Item -ItemType Directory -Force -Path (Join-Path $source 'backup') | Out-Null
Copy-Item -LiteralPath $found -Destination (Join-Path $source 'backup\Assembly-CSharp.dll') -Force
& (Join-Path $source 'Build.ps1') -GameDirectory $game
if(!$?) {throw 'Source build failed'}
& (Join-Path $source 'tests\RunPolicies.ps1')
if(!$?) {throw 'Policy tests failed'}
& (Join-Path $source 'tests\VerifyHooks.ps1') -AssemblyPath (Join-Path $source 'build\Assembly-CSharp.dll')
if(!$?) {throw 'Hook checks failed'}
Write-Host ('Build verified: '+(Join-Path $source 'build')) -ForegroundColor Green
Write-Host 'The installed player and public payload were not modified. Rebuilt files can have different compiler-generated hashes.'
