param([Parameter(Mandatory=$true)][string]$PackageRoot,[Parameter(Mandatory=$true)][string]$OriginalAssembly)
$ErrorActionPreference='Stop'
. (Join-Path $PackageRoot 'tools\ReleaseCore.ps1')
. (Join-Path $PackageRoot 'tools\Manage-SteamUpdateLock.ps1')
$root=Join-Path ([IO.Path]::GetTempPath()) ('SKYBOX update lock fixture '+[guid]::NewGuid().ToString('N'))
$apps=Join-Path $root 'steamapps'
$game=Join-Path $apps 'common\SKYBOX VR Video Player'
$managed=Join-Path $game 'SteamVR_SourceVRPlayer_Data\Managed'
New-Item -ItemType Directory -Force -Path $managed | Out-Null
[IO.File]::WriteAllText((Join-Path $game 'SteamVR_SourceVRPlayer.exe'),'fixture: never executed')
Copy-Item -LiteralPath $OriginalAssembly -Destination (Join-Path $managed 'Assembly-CSharp.dll')
$manifest=Join-Path $apps 'appmanifest_721090.acf'
[IO.File]::WriteAllText($manifest,"`"AppState`"`r`n{`r`n`t`"appid`"`t`"721090`"`r`n`t`"AutoUpdateBehavior`"`t`"2`"`r`n}`r`n")
$attributes=[int](Get-Item -LiteralPath $manifest).Attributes
$locked=$false
try {
    $result=Invoke-SkyboxUpdateLock -GameDirectory $game
    $locked=$true
    if($result-ne'Locked'){throw 'Lock did not complete'}
    if(!((Get-Item -LiteralPath $manifest -Force).Attributes-band[IO.FileAttributes]::ReadOnly)){throw 'Manifest not read-only'}
    if((Get-Content -LiteralPath $manifest -Raw)-notmatch'"AutoUpdateBehavior"\s+"1"'){throw 'Update policy not changed'}
    foreach($directory in @('downloading\721090','temp\721090')) {
        $target=Join-Path $apps $directory
        $rejected=$false
        try{[IO.File]::WriteAllText((Join-Path $target 'probe.txt'),'test')}catch [UnauthorizedAccessException]{$rejected=$true}
        if(!$rejected){throw 'Update directory write was not denied'}
    }
    'PASS update lock denies writes in both app-specific directories'
    $null=Invoke-SkyboxUpdateLock -GameDirectory $game -Unlock
    $locked=$false
    if([int](Get-Item -LiteralPath $manifest -Force).Attributes-ne$attributes){throw 'Manifest attributes not restored'}
    if((Get-Content -LiteralPath $manifest -Raw)-notmatch'"AutoUpdateBehavior"\s+"2"'){throw 'Original update policy not restored'}
    $state=Get-Content -LiteralPath (Join-Path $game 'SkyboxUsability.Backup\steam-update-lock.json') -Raw | ConvertFrom-Json
    foreach($entry in $state.Directories) {
        $sddl=(Get-Acl -LiteralPath $entry.Path).GetSecurityDescriptorSddlForm([Security.AccessControl.AccessControlSections]::Access)
        if($sddl-ne$entry.AccessSddl){throw 'Original access permissions not restored exactly'}
        [IO.File]::WriteAllText((Join-Path $entry.Path 'probe.txt'),'restored')
    }
    'PASS unlock restores original ACL, manifest attributes and update policy'
} finally {
    if($locked){$null=Invoke-SkyboxUpdateLock -GameDirectory $game -Unlock}
}
"Update-lock fixture retained: $root"
