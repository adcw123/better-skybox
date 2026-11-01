# Compatible with Windows PowerShell 5.1; no compilation or network is needed.
Set-StrictMode -Version 2.0
function Get-SkyboxHash([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function Assert-SkyboxPayload([string]$PackageRoot) {
    $manifestPath=Join-Path $PackageRoot 'release.json'
    $release=Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach($field in @('OriginalAssemblyHash','PatchedAssemblyHash','ExtensionHash')) {
        if($release.$field -notmatch '^[A-Fa-f0-9]{64}$') { throw "Invalid release manifest: $field" }
    }
    foreach($name in @('PatchAssembly.exe','dnlib.dll','SkyboxUsability.dll')) {
        $entry=$release.Payload.PSObject.Properties[$name]
        $path=Join-Path (Join-Path $PackageRoot 'payload') $name
        if($null-eq$entry-or!(Test-Path -LiteralPath $path -PathType Leaf)-or(Get-SkyboxHash $path)-ne$entry.Value) {
            throw "Package file missing or damaged: $name. Extract the complete ZIP again."
        }
    }
    return $release
}
function Test-SkyboxRunning([string]$GameDirectory) {
    $expected=[IO.Path]::GetFullPath((Join-Path $GameDirectory 'SteamVR_SourceVRPlayer.exe'))
    foreach($process in @(Get-Process SteamVR_SourceVRPlayer -ErrorAction SilentlyContinue)) {
        try { $path=$process.Path } catch { return $true }
        if(!$path-or[string]::Equals([IO.Path]::GetFullPath($path),$expected,[StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}
function Resolve-SkyboxDirectory([string]$GameDirectory) {
    $full=[IO.Path]::GetFullPath((Resolve-Path -LiteralPath $GameDirectory -ErrorAction Stop).Path)
    foreach($relative in @('SteamVR_SourceVRPlayer.exe','SteamVR_SourceVRPlayer_Data\Managed\Assembly-CSharp.dll')) {
        if(!(Test-Path -LiteralPath (Join-Path $full $relative) -PathType Leaf)) { throw "Not a SKYBOX installation directory: $full" }
    }
    return $full
}
function Invoke-SkyboxPatch {
    param([Parameter(Mandatory=$true)][string]$PackageRoot,[Parameter(Mandatory=$true)][string]$GameDirectory,
          [ValidateSet('Install','Uninstall')][string]$Mode='Install',[string]$OriginalAssembly)
    $ErrorActionPreference='Stop'
    $release=Assert-SkyboxPayload $PackageRoot
    $game=Resolve-SkyboxDirectory $GameDirectory
    if(Test-SkyboxRunning $game) { throw 'Close SKYBOX completely before installing or uninstalling.' }
    $managed=Join-Path $game 'SteamVR_SourceVRPlayer_Data\Managed'
    $target=Join-Path $managed 'Assembly-CSharp.dll'
    $extension=Join-Path $managed 'SkyboxUsability.dll'
    $backup=Join-Path $game 'SkyboxUsability.Backup'
    $originalBackup=Join-Path $backup 'Assembly-CSharp.original.dll'
    $current=Get-SkyboxHash $target
    $currentExtension=if(Test-Path -LiteralPath $extension) {Get-SkyboxHash $extension} else {''}
    $known=@($release.OriginalAssemblyHash,$release.PatchedAssemblyHash)+@($release.KnownPreviousAssemblyHashes)
    if($current-notin$known) { throw 'Unsupported or modified SKYBOX assembly. No installation files were changed. Supported: Steam Windows 1.1.8 (2668).' }
    if((Test-Path -LiteralPath $backup)-and((Get-Item -LiteralPath $backup -Force).Attributes-band[IO.FileAttributes]::ReparsePoint)) {throw 'Backup directory must not be a link.'}
    $result=@{Action=$Mode;Version=$release.Version;GameDirectory=$game;BackupDirectory=$backup;Status=''}
    if($Mode-eq'Uninstall') {
        if($current-eq$release.OriginalAssemblyHash-and!$currentExtension) {$result.Status='AlreadyUninstalled';return [pscustomobject]$result}
        if($currentExtension-and$currentExtension-ne$release.ExtensionHash) {throw 'Extension differs from this release. Refusing to remove an unrecognized extension.'}
        if($current-ne$release.OriginalAssemblyHash) {
            if(!(Test-Path -LiteralPath $originalBackup)-or(Get-SkyboxHash $originalBackup)-ne$release.OriginalAssemblyHash) {
                throw 'Verified original backup not found. Restore the original with your previous installer or Steam Verify integrity, then remove this extension.'
            }
            Copy-Item -LiteralPath $originalBackup -Destination $target -Force
            if((Get-SkyboxHash $target)-ne$release.OriginalAssemblyHash) {throw 'Original restoration verification failed.'}
        }
        if($currentExtension) {Remove-Item -LiteralPath $extension -Force}
        $result.Status='Uninstalled';return [pscustomobject]$result
    }
    if($current-eq$release.PatchedAssemblyHash-and$currentExtension-eq$release.ExtensionHash) {$result.Status='AlreadyInstalled';return [pscustomobject]$result}
    if($current-eq$release.OriginalAssemblyHash-and$currentExtension-and$currentExtension-ne$release.ExtensionHash) {throw 'An unrecognized SkyboxUsability.dll already exists. No installation files were changed.'}
    $source=$null
    foreach($candidate in @($OriginalAssembly,$originalBackup)) {
        if($candidate-and(Test-Path -LiteralPath $candidate -PathType Leaf)-and(Get-SkyboxHash $candidate)-eq$release.OriginalAssemblyHash) {$source=$candidate;break}
    }
    if($current-eq$release.OriginalAssemblyHash) {$source=$target}
    if(!$source-and$current-ne$release.PatchedAssemblyHash) {throw 'Upgrading an older patch requires its verified original backup. Uninstall the older patch first, or supply -OriginalAssembly.'}
    if((Test-Path -LiteralPath $originalBackup)-and(Get-SkyboxHash $originalBackup)-ne$release.OriginalAssemblyHash) {throw 'Original backup hash mismatch. Refusing to overwrite this backup.'}
    New-Item -ItemType Directory -Force -Path $backup | Out-Null
    if($source-and!(Test-Path -LiteralPath $originalBackup)) {
        Copy-Item -LiteralPath $source -Destination $originalBackup
        if((Get-SkyboxHash $originalBackup)-ne$release.OriginalAssemblyHash) {throw 'Original backup verification failed.'}
        $source=$originalBackup
    }
    $stage=Join-Path $backup ('.stage-'+[guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    $stageMain=Join-Path $stage 'Assembly-CSharp.dll'
    $stageExtension=Join-Path $stage 'SkyboxUsability.dll'
    $rollbackMain=Join-Path $backup ('before-'+$current+'.dll')
    $rollbackExtension=if($currentExtension) {Join-Path $backup ('extension-before-'+$currentExtension+'.dll')} else {$null}
    $changed=$false
    try {
        if($current-eq$release.PatchedAssemblyHash) {Copy-Item -LiteralPath $target -Destination $stageMain}
        else {
            $patcher=Join-Path $PackageRoot 'payload\PatchAssembly.exe'
            $output=& $patcher $source (Join-Path $PackageRoot 'payload\SkyboxUsability.dll') $stageMain 2>&1
            if($LASTEXITCODE) {throw ('Patch generation failed: '+($output-join[Environment]::NewLine))}
        }
        if((Get-SkyboxHash $stageMain)-ne$release.PatchedAssemblyHash) {throw 'Generated assembly differs from this release. No DLLs were installed.'}
        Copy-Item -LiteralPath (Join-Path $PackageRoot 'payload\SkyboxUsability.dll') -Destination $stageExtension
        if(Test-SkyboxRunning $game) {throw 'SKYBOX started during installation. Close it and retry.'}
        if((Get-SkyboxHash $target)-ne$current) {throw 'Assembly changed during installation. Refusing to overwrite it.'}
        Copy-Item -LiteralPath $target -Destination $rollbackMain -Force
        if($rollbackExtension) {Copy-Item -LiteralPath $extension -Destination $rollbackExtension -Force}
        $changed=$true
        Copy-Item -LiteralPath $stageExtension -Destination $extension -Force
        Copy-Item -LiteralPath $stageMain -Destination $target -Force
        if((Get-SkyboxHash $target)-ne$release.PatchedAssemblyHash-or(Get-SkyboxHash $extension)-ne$release.ExtensionHash) {throw 'Installed DLL hash mismatch.'}
        $result.Status='Installed'
        return [pscustomobject]$result
    } catch {
        if($changed) {
            Copy-Item -LiteralPath $rollbackMain -Destination $target -Force
            if($rollbackExtension) {Copy-Item -LiteralPath $rollbackExtension -Destination $extension -Force}
            elseif(Test-Path -LiteralPath $extension) {Remove-Item -LiteralPath $extension -Force}
        }
        throw
    } finally {
        # Delete only the two fixed staging files and then the empty stage folder.
        foreach($file in @($stageMain,$stageExtension)) {if(Test-Path -LiteralPath $file) {Remove-Item -LiteralPath $file -Force}}
        if(Test-Path -LiteralPath $stage) {Remove-Item -LiteralPath $stage -Force}
    }
}
function Find-SkyboxDirectories {
    $steamRoots=New-Object 'System.Collections.Generic.List[string]'
    foreach($key in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam','HKLM:\SOFTWARE\Valve\Steam')) {
        if(Test-Path $key) {
            $item=Get-ItemProperty $key
            foreach($property in @('SteamPath','InstallPath')) {if($item.PSObject.Properties[$property]-and$item.$property) {$steamRoots.Add([string]$item.$property)}}
        }
    }
    if(${env:ProgramFiles(x86)}) {$steamRoots.Add((Join-Path ${env:ProgramFiles(x86)} 'Steam'))}
    $libraries=New-Object 'System.Collections.Generic.List[string]'
    foreach($root in @($steamRoots | Select-Object -Unique)) {
        if(![IO.Directory]::Exists($root)) {continue}
        $libraries.Add($root)
        $vdf=Join-Path $root 'steamapps\libraryfolders.vdf'
        if(Test-Path -LiteralPath $vdf) {
            $text=Get-Content -LiteralPath $vdf -Raw -Encoding UTF8
            foreach($match in [regex]::Matches($text,'"path"\s+"((?:\\.|[^"\\])*)"')) {$libraries.Add($match.Groups[1].Value.Replace('\\','\'))}
        }
    }
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($library in @($libraries | Select-Object -Unique)) {
        if(![IO.Directory]::Exists($library)) {continue}
        $game=Join-Path $library 'steamapps\common\SKYBOX VR Video Player'
        if(Test-Path -LiteralPath (Join-Path $game 'SteamVR_SourceVRPlayer.exe')) {
            $full=[IO.Path]::GetFullPath($game)
            if($seen.Add($full)) {$full}
        }
    }
}
