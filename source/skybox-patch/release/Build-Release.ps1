param([string]$OutputDirectory,[switch]$Force)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
if(!$OutputDirectory) {$OutputDirectory=Join-Path (Split-Path $project -Parent) 'dist'}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$name='SKYBOX-Usability-v1.3.0-Windows-offline'
$zip=Join-Path $OutputDirectory ($name+'.zip')
if((Test-Path -LiteralPath $zip)-and!$Force) {throw 'ZIP already exists. Use -Force to replace this generated release.'}
$stageParent=Join-Path $OutputDirectory ('staging-'+[guid]::NewGuid().ToString('N'))
$stage=Join-Path $stageParent $name
New-Item -ItemType Directory -Force -Path $stage | Out-Null
function Copy-One([string]$From,[string]$To) {
    New-Item -ItemType Directory -Force -Path (Split-Path $To -Parent) | Out-Null
    Copy-Item -LiteralPath $From -Destination $To
}
function Write-Utf8([string]$Path,[string]$Text) {
    New-Item -ItemType Directory -Force -Path (Split-Path $Path -Parent) | Out-Null
    [IO.File]::WriteAllText($Path,$Text,[Text.UTF8Encoding]::new($true))
}
foreach($file in @('README.md','CHANGELOG.md','LICENSE')) {Copy-One (Join-Path $PSScriptRoot $file) (Join-Path $stage $file)}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'docs') -File) {Copy-One $file.FullName (Join-Path $stage ('docs\'+$file.Name))}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tools') -File -Filter '*.ps1') {Write-Utf8 (Join-Path $stage ('tools\'+$file.Name)) ([IO.File]::ReadAllText($file.FullName))}
$launchers=@{
    'Install.cmd'='Install';'Uninstall.cmd'='Uninstall';'Launch-SKYBOX.cmd'='Launch';
    'Lock-Steam-Updates.cmd'='LockUpdates';'Unlock-Steam-Updates.cmd'='UnlockUpdates'
}
foreach($launcher in $launchers.Keys) {
    $action=$launchers[$launcher]
    $lines=@('@echo off','chcp 65001 >nul',('powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0tools\Manage-SKYBOX.ps1" -Action '+$action),'set "SKYBOX_EXIT=%ERRORLEVEL%"')
    if($action-eq'Launch') {$lines+='if not "%SKYBOX_EXIT%"=="0" pause'} else {$lines+='pause'}
    $lines+='exit /b %SKYBOX_EXIT%'
    [IO.File]::WriteAllText((Join-Path $stage $launcher),($lines-join"`r`n")+"`r`n",[Text.UTF8Encoding]::new($false))
}
foreach($file in @('PatchAssembly.exe','dnlib.dll','SkyboxUsability.dll')) {Copy-One (Join-Path $project ('build\'+$file)) (Join-Path $stage ('payload\'+$file))}
Copy-One (Join-Path $project 'tools\dnlib\LICENSE.txt') (Join-Path $stage 'licenses\dnlib-LICENSE.txt')
Copy-One (Join-Path $PSScriptRoot 'Roslyn-License.txt') (Join-Path $stage 'licenses\Roslyn-LICENSE.txt')
Copy-One (Join-Path $project 'tools\roslyn\ThirdPartyNotices.rtf') (Join-Path $stage 'licenses\Roslyn-ThirdPartyNotices.rtf')
$developer=Join-Path $stage 'source\skybox-patch'
foreach($file in @('Build.ps1','PatchAssembly.cs','Install.ps1')) {Copy-One (Join-Path $project $file) (Join-Path $developer $file)}
foreach($folder in @('src','tests')) {
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $project $folder) -File) {
        if($file.Extension-in@('.cs','.ps1')) {Copy-One $file.FullName (Join-Path $developer ($folder+'\'+$file.Name))}
    }
}
Copy-One (Join-Path $project 'tools\dnlib\lib\net45\dnlib.dll') (Join-Path $developer 'tools\dnlib\lib\net45\dnlib.dll')
Copy-One (Join-Path $project 'tools\dnlib\LICENSE.txt') (Join-Path $developer 'tools\dnlib\LICENSE.txt')
Copy-One (Join-Path $project 'tools\roslyn\ThirdPartyNotices.rtf') (Join-Path $developer 'tools\roslyn\ThirdPartyNotices.rtf')
$compilerRoot=Join-Path $project 'tools\roslyn\tasks\net472'
foreach($file in Get-ChildItem -LiteralPath $compilerRoot -Recurse -File) {
    $relative=$file.FullName.Substring($compilerRoot.Length).TrimStart('\','/')
    Copy-One $file.FullName (Join-Path $developer ('tools\roslyn\tasks\net472\'+$relative))
}
# Include packaging source so the release can be rebuilt without private files.
foreach($file in Get-ChildItem -LiteralPath $PSScriptRoot -File) {
    if($file.Extension-in@('.ps1','.md','.txt')-or$file.Name-eq'LICENSE') {Copy-One $file.FullName (Join-Path $developer ('release\'+$file.Name))}
}
foreach($folder in @('tools','docs')) {
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $folder) -File) {Copy-One $file.FullName (Join-Path $developer ('release\'+$folder+'\'+$file.Name))}
}
foreach($script in Get-ChildItem -LiteralPath $stage -Recurse -File -Filter '*.ps1') {Write-Utf8 $script.FullName ([IO.File]::ReadAllText($script.FullName))}
$payload=@{}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $stage 'payload') -File) {$payload[$file.Name]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}
$manifest=@{
    Version='1.3.0';Date='2026-10-31';Target='Steam Windows SKYBOX 1.1.8 (2668)';AppId=721090;
    OriginalAssemblyHash='B6DAD50B364B271BE990F9254BC3EDBBD1CF1BC5455DBF11CB0AC311583C5130';
    PatchedAssemblyHash=(Get-FileHash -LiteralPath (Join-Path $project 'build\Assembly-CSharp.dll')).Hash;
    ExtensionHash=$payload['SkyboxUsability.dll'];Payload=$payload;
    KnownPreviousAssemblyHashes=@('2F92C9CE326186994034BDB3E99E64DADCFEBC0523D8BEA3AE4A99DECA7BCEE7','9AD0A1D224D5E77473C50528B4C4821C3B4F0CAA0CE6E671D14C09C86F1B6C06','8135BD187CCA2A0D22A668A18496EEF5A08BC66E0F9C24A2A198220E86510E00')
}
Write-Utf8 (Join-Path $stage 'release.json') ($manifest | ConvertTo-Json -Depth 6)
Write-Utf8 (Join-Path $stage '.gitignore') "**/backup/`r`n**/build/`r`nSkyboxUsability.Backup/`r`n*.log`r`n**/playback-defaults.json`r`n**/folder-formats.json`r`n**/scoped-playback.json`r`n**/completed-videos.json`r`n"
$files=@(Get-ChildItem -LiteralPath $stage -Recurse -File -Force)
if(@($files | Where-Object {$_.Name-eq'Assembly-CSharp.dll'-or$_.Name-eq'SteamVR_SourceVRPlayer.exe'-or$_.Extension-in@('.png','.mp4','.acf','.vdf','.log')}).Count) {throw 'Unexpected game binary, media or private state in release.'}
$sums=@()
foreach($file in $files | Sort-Object FullName) {$relative=$file.FullName.Substring($stage.Length+1).Replace('\','/');$sums+=((Get-FileHash -LiteralPath $file.FullName).Hash+'  '+$relative)}
Write-Utf8 (Join-Path $stage 'SHA256SUMS.txt') (($sums-join"`r`n")+"`r`n")
Compress-Archive -LiteralPath $stage -DestinationPath $zip -CompressionLevel Optimal -Force
$zipHash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
[IO.File]::WriteAllText(($zip+'.sha256'),($zipHash+'  '+(Split-Path $zip -Leaf)+"`r`n"),[Text.UTF8Encoding]::new($false))
[pscustomobject]@{Zip=$zip;Sha256=$zipHash;Bytes=(Get-Item -LiteralPath $zip).Length;StagingDirectory=$stage;Files=$files.Count+1}
