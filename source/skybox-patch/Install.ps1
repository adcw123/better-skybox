param([switch]$Uninstall,[string]$GameDirectory='C:\Program Files (x86)\Steam\steamapps\common\SKYBOX VR Video Player')
$ErrorActionPreference='Stop'
$managed=Join-Path $GameDirectory 'SteamVR_SourceVRPlayer_Data\Managed'
$target=Join-Path $managed 'Assembly-CSharp.dll'
$original='B6DAD50B364B271BE990F9254BC3EDBBD1CF1BC5455DBF11CB0AC311583C5130'
$backup=Join-Path $PSScriptRoot 'backup\Assembly-CSharp.dll'
$built=Join-Path $PSScriptRoot 'build\Assembly-CSharp.dll'
$extension=Join-Path $managed 'SkyboxUsability.dll'
$expectedExecutable=[IO.Path]::GetFullPath((Join-Path $GameDirectory 'SteamVR_SourceVRPlayer.exe'))
$running=@(Get-Process SteamVR_SourceVRPlayer -ErrorAction SilentlyContinue | Where-Object { $_.Path -and [string]::Equals([IO.Path]::GetFullPath($_.Path),$expectedExecutable,[StringComparison]::OrdinalIgnoreCase) })
if($running.Count) {throw 'Close SKYBOX before installing or restoring.'}
if((Get-FileHash -LiteralPath $backup).Hash -ne $original) {throw 'Original backup hash mismatch'}
$current=(Get-FileHash -LiteralPath $target).Hash
$patched=(Get-FileHash -LiteralPath $built).Hash
$previous=Join-Path $PSScriptRoot 'backup\installed-v1.dll'
$previousHash=if(Test-Path -LiteralPath $previous) {(Get-FileHash -LiteralPath $previous).Hash} else {''}
$acceptedV1='2F92C9CE326186994034BDB3E99E64DADCFEBC0523D8BEA3AE4A99DECA7BCEE7'
$acceptedV11='9AD0A1D224D5E77473C50528B4C4821C3B4F0CAA0CE6E671D14C09C86F1B6C06'
$acceptedV12='8135BD187CCA2A0D22A668A18496EEF5A08BC66E0F9C24A2A198220E86510E00'
if($current -ne $original -and $current -ne $patched -and $current -ne $previousHash -and $current -ne $acceptedV1 -and $current -ne $acceptedV11 -and $current -ne $acceptedV12) {throw 'Unrecognized SKYBOX assembly; no files changed.'}
if($Uninstall) {
    Copy-Item -LiteralPath $backup -Destination $target -Force
    if(Test-Path -LiteralPath $extension) {Remove-Item -LiteralPath $extension}
    if((Get-FileHash -LiteralPath $target).Hash -ne $original) {throw 'Restoration verification failed'}
    Write-Output 'Restored verified original assembly.'
} else {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'build\SkyboxUsability.dll') -Destination $extension -Force
    Copy-Item -LiteralPath $built -Destination $target -Force
    if((Get-FileHash -LiteralPath $target).Hash -ne $patched) {throw 'Installation verification failed'}
    Write-Output 'Installed patched assembly and SkyboxUsability.dll; original backup retained.'
}
