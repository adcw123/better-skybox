param([Parameter(Mandatory=$true)][string]$PackageRoot,[Parameter(Mandatory=$true)][string]$OriginalAssembly)
$ErrorActionPreference='Stop'
. (Join-Path $PackageRoot 'tools\ReleaseCore.ps1')
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('SKYBOX 发布 测试 '+[guid]::NewGuid().ToString('N'))
$game=Join-Path $testRoot 'Steam 库\steamapps\common\SKYBOX VR Video Player'
$managed=Join-Path $game 'SteamVR_SourceVRPlayer_Data\Managed'
New-Item -ItemType Directory -Force -Path $managed | Out-Null
[IO.File]::WriteAllText((Join-Path $game 'SteamVR_SourceVRPlayer.exe'),'fixture: never executed')
$target=Join-Path $managed 'Assembly-CSharp.dll'
$extension=Join-Path $managed 'SkyboxUsability.dll'
Copy-Item -LiteralPath $OriginalAssembly -Destination $target
$manifest=Get-Content -LiteralPath (Join-Path $PackageRoot 'release.json') -Raw | ConvertFrom-Json
$result=Invoke-SkyboxPatch -PackageRoot $PackageRoot -GameDirectory $game -Mode Install
if($result.Status-ne'Installed'){throw 'Fresh install did not complete'}
if((Get-FileHash -LiteralPath $target).Hash-ne$manifest.PatchedAssemblyHash){throw 'Patched assembly hash differs'}
if((Get-FileHash -LiteralPath $extension).Hash-ne$manifest.ExtensionHash){throw 'Extension hash differs'}
'PASS fresh installation from legitimate original matches released build'
$backup=Join-Path $game 'SkyboxUsability.Backup\Assembly-CSharp.original.dll'
if((Get-FileHash -LiteralPath $backup).Hash-ne$manifest.OriginalAssemblyHash){throw 'Original backup differs'}
'PASS original bytes backed up locally'
$result=Invoke-SkyboxPatch -PackageRoot $PackageRoot -GameDirectory $game -Mode Install
if($result.Status-ne'AlreadyInstalled'){throw 'Repeat installation not idempotent'}
'PASS repeat installation is idempotent'
$private=Join-Path $game 'private-user-settings.json'
[IO.File]::WriteAllText($private,'keep-user-settings')
$result=Invoke-SkyboxPatch -PackageRoot $PackageRoot -GameDirectory $game -Mode Uninstall
if((Get-FileHash -LiteralPath $target).Hash-ne$manifest.OriginalAssemblyHash-or(Test-Path -LiteralPath $extension)){throw 'Uninstall did not restore exact original'}
if([IO.File]::ReadAllText($private)-ne'keep-user-settings'){throw 'Uninstall changed unrelated data'}
'PASS uninstall restores original, removes own extension, preserves other data'
$result=Invoke-SkyboxPatch -PackageRoot $PackageRoot -GameDirectory $game -Mode Uninstall
if($result.Status-ne'AlreadyUninstalled'){throw 'Repeat uninstall not idempotent'}
'PASS repeat uninstall is idempotent'
[IO.File]::WriteAllText($target,'unknown-version')
$rejected=$false
try {Invoke-SkyboxPatch -PackageRoot $PackageRoot -GameDirectory $game -Mode Install | Out-Null} catch {$rejected=$true}
if(!$rejected-or[IO.File]::ReadAllText($target)-ne'unknown-version'){throw 'Unknown version was modified'}
'PASS unknown version refused without changing original'
Copy-Item -LiteralPath $OriginalAssembly -Destination $target -Force
[IO.File]::WriteAllText($extension,'another-extension')
$rejected=$false
try {Invoke-SkyboxPatch -PackageRoot $PackageRoot -GameDirectory $game -Mode Install | Out-Null} catch {$rejected=$true}
if(!$rejected-or[IO.File]::ReadAllText($extension)-ne'another-extension'){throw 'Unrelated extension was overwritten'}
'PASS unrelated extension refused'
Remove-Item -LiteralPath $extension
$savedHash=(Get-FileHash -LiteralPath $target).Hash
$payload=Join-Path $PackageRoot 'payload\SkyboxUsability.dll'
$payloadBytes=[IO.File]::ReadAllBytes($payload)
try{
    [IO.File]::WriteAllText($payload,'corrupt-payload')
    $rejected=$false
    try {Invoke-SkyboxPatch -PackageRoot $PackageRoot -GameDirectory $game -Mode Install | Out-Null} catch {$rejected=$true}
    if(!$rejected-or(Get-FileHash -LiteralPath $target).Hash-ne$savedHash){throw 'Corrupt payload was accepted'}
    'PASS corrupt payload refused before changing installation'
}finally{[IO.File]::WriteAllBytes($payload,$payloadBytes)}
"Release fixture retained: $testRoot"
