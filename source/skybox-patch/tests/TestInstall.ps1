$ErrorActionPreference='Stop'
$patchRoot=Split-Path -Parent $PSScriptRoot
$fixture=Join-Path $patchRoot ('build\install-test-'+[guid]::NewGuid().ToString('N'))
$managed=Join-Path $fixture 'SteamVR_SourceVRPlayer_Data\Managed'
New-Item -ItemType Directory -Force -Path $managed | Out-Null
$target=Join-Path $managed 'Assembly-CSharp.dll'
Copy-Item -LiteralPath "$patchRoot\backup\Assembly-CSharp.dll" -Destination $target
$original=(Get-FileHash -LiteralPath $target).Hash
& "$patchRoot\Install.ps1" -GameDirectory $fixture
if((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath "$patchRoot\build\Assembly-CSharp.dll").Hash) {throw 'Installed assembly mismatch'}
if((Get-FileHash -LiteralPath "$managed\SkyboxUsability.dll").Hash -ne (Get-FileHash -LiteralPath "$patchRoot\build\SkyboxUsability.dll").Hash) {throw 'Installed extension mismatch'}
Write-Output 'PASS installation hashes match built artifacts'
& "$patchRoot\Install.ps1" -GameDirectory $fixture -Uninstall
if((Get-FileHash -LiteralPath $target).Hash -ne $original) {throw 'Uninstall did not restore original bytes'}
if(Test-Path -LiteralPath "$managed\SkyboxUsability.dll") {throw 'Uninstall left extension installed'}
Write-Output 'PASS uninstall restores exact original and removes extension'
[IO.File]::WriteAllText($target,'unrecognized assembly test fixture')
$rejected=$false
try { & "$patchRoot\Install.ps1" -GameDirectory $fixture } catch {if($_.Exception.Message -notmatch 'Unrecognized SKYBOX') {throw}; $rejected=$true}
if(!$rejected) {throw 'Unknown assembly was accepted'}
if([IO.File]::ReadAllText($target) -ne 'unrecognized assembly test fixture') {throw 'Unknown assembly was modified'}
Write-Output 'PASS unknown assembly refused without changing target'
Write-Output "Test fixture retained at $fixture"
