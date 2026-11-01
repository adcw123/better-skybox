function Invoke-SkyboxUpdateLock {
    param([string]$GameDirectory,[switch]$Unlock)
    $ErrorActionPreference='Stop'
    $game=Resolve-SkyboxDirectory $GameDirectory
    $common=Split-Path $game -Parent
    $steamApps=Split-Path $common -Parent
    if((Split-Path $common -Leaf)-ne'common'-or(Split-Path $steamApps -Leaf)-ne'steamapps') {throw 'Update locking requires a standard Steam library installation.'}
    $manifest=Join-Path $steamApps 'appmanifest_721090.acf'
    $text=Get-Content -LiteralPath $manifest -Raw -Encoding UTF8
    if($text-notmatch'"appid"\s+"721090"') {throw 'Steam manifest app ID mismatch.'}
    $targets=@((Join-Path $steamApps 'downloading\721090'),(Join-Path $steamApps 'temp\721090'))
    $backup=Join-Path $game 'SkyboxUsability.Backup'
    if((Test-Path -LiteralPath $backup)-and((Get-Item -LiteralPath $backup -Force).Attributes-band[IO.FileAttributes]::ReparsePoint)) {throw 'Backup directory must not be a link.'}
    $statePath=Join-Path $backup 'steam-update-lock.json'
    $access=[Security.AccessControl.AccessControlSections]::Access
    if($Unlock) {
        if(!(Test-Path -LiteralPath $statePath)) {throw 'No update-lock backup from this tool. Use the previous locking tool to restore its permissions.'}
        $state=Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
        if($state.Manifest-ne$manifest-or@($state.Directories).Count-ne2) {throw 'Update lock backup does not match this installation.'}
        foreach($entry in $state.Directories) {if($entry.Path-notin$targets) {throw 'Unexpected ACL backup target.'}}
        foreach($entry in $state.Directories) {
            $acl=Get-Acl -LiteralPath $entry.Path
            $acl.SetSecurityDescriptorSddlForm($entry.AccessSddl,$access)
            Set-Acl -LiteralPath $entry.Path -AclObject $acl
        }
        $attributes=[int](Get-Item -LiteralPath $manifest -Force).Attributes
        [IO.File]::SetAttributes($manifest,[IO.FileAttributes]($attributes-band(-bnot[int][IO.FileAttributes]::ReadOnly)))
        # A MatchEvaluator avoids ambiguity when a numeric replacement follows $1.
        $value=[string]$state.AutoUpdateBehavior
        $text=[regex]::Replace($text,'("AutoUpdateBehavior"\s+")\d+(")',[Text.RegularExpressions.MatchEvaluator]{param($m) $m.Groups[1].Value+$value+$m.Groups[2].Value})
        [IO.File]::WriteAllText($manifest,$text,(New-Object Text.UTF8Encoding($false)))
        [IO.File]::SetAttributes($manifest,[IO.FileAttributes][int]$state.ManifestAttributes)
        return 'Unlocked'
    }
    if(Test-Path -LiteralPath $statePath) {
        $state=Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
        if($state.Manifest-ne$manifest) {throw 'Existing update backup belongs to a different installation.'}
    } else {
        $auto=[regex]::Match($text,'"AutoUpdateBehavior"\s+"(\d+)"')
        if(!$auto.Success) {throw 'Steam update policy field was not found.'}
        $entries=@()
        foreach($target in $targets) {
            if(!(Test-Path -LiteralPath $target)) {New-Item -ItemType Directory -Path $target | Out-Null}
            if((Get-Item -LiteralPath $target -Force).Attributes-band[IO.FileAttributes]::ReparsePoint) {throw 'An update directory is a link.'}
            if(@(Get-ChildItem -LiteralPath $target -Force).Count) {throw 'An update directory contains an active download. Finish or cancel it in Steam first.'}
            $acl=Get-Acl -LiteralPath $target
            $existing=@($acl.Access | Where-Object {$_.AccessControlType-eq'Deny'-and($_.FileSystemRights-band[Security.AccessControl.FileSystemRights]::Write)})
            if($existing.Count) {throw 'An update directory is already blocked by another tool. Restore that tool first.'}
            $entries+=@{Path=$target;AccessSddl=$acl.GetSecurityDescriptorSddlForm($access)}
        }
        New-Item -ItemType Directory -Force -Path $backup | Out-Null
        $state=@{Manifest=$manifest;ManifestAttributes=[int](Get-Item -LiteralPath $manifest -Force).Attributes;AutoUpdateBehavior=$auto.Groups[1].Value;Directories=$entries}
        $state | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statePath -Encoding UTF8
    }
    $sid=[Security.Principal.SecurityIdentifier]::new('S-1-1-0')
    $rights=[Security.AccessControl.FileSystemRights]::Write -bor [Security.AccessControl.FileSystemRights]::Delete -bor [Security.AccessControl.FileSystemRights]::DeleteSubdirectoriesAndFiles
    $inherit=[Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit
    foreach($target in $targets) {
        $acl=Get-Acl -LiteralPath $target
        $rule=[Security.AccessControl.FileSystemAccessRule]::new($sid,$rights,$inherit,[Security.AccessControl.PropagationFlags]::None,[Security.AccessControl.AccessControlType]::Deny)
        $acl.SetAccessRule($rule);Set-Acl -LiteralPath $target -AclObject $acl
    }
    $attributes=[int](Get-Item -LiteralPath $manifest -Force).Attributes
    [IO.File]::SetAttributes($manifest,[IO.FileAttributes]($attributes-band(-bnot[int][IO.FileAttributes]::ReadOnly)))
    $text=[regex]::Replace($text,'("AutoUpdateBehavior"\s+")\d+(")',[Text.RegularExpressions.MatchEvaluator]{param($m) $m.Groups[1].Value+'1'+$m.Groups[2].Value})
    [IO.File]::WriteAllText($manifest,$text,(New-Object Text.UTF8Encoding($false)))
    [IO.File]::SetAttributes($manifest,[IO.FileAttributes]($attributes-bor[int][IO.FileAttributes]::ReadOnly))
    return 'Locked'
}
