param([ValidateSet('Install','Uninstall','Launch','LockUpdates','UnlockUpdates')][string]$Action='Install',[string]$GameDirectory,[string]$OriginalAssembly,[switch]$Elevated)
$ErrorActionPreference='Stop'
$package=Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'ReleaseCore.ps1')
. (Join-Path $PSScriptRoot 'Manage-SteamUpdateLock.ps1')
try {
    if(!$GameDirectory) {
        $found=@(Find-SkyboxDirectories)
        if($found.Count-eq1) {$GameDirectory=$found[0]}
        else {
            Add-Type -AssemblyName System.Windows.Forms
            $dialog=New-Object Windows.Forms.FolderBrowserDialog
            $dialog.Description='请选择 SKYBOX 安装文件夹（里面应有 SteamVR_SourceVRPlayer.exe）'
            $dialog.ShowNewFolderButton=$false
            if($dialog.ShowDialog()-ne[Windows.Forms.DialogResult]::OK) {throw '已取消选择，没有修改任何安装文件。'}
            $GameDirectory=$dialog.SelectedPath
            $dialog.Dispose()
        }
    }
    $GameDirectory=Resolve-SkyboxDirectory $GameDirectory
    $null=Assert-SkyboxPayload $package
    Write-Host ('SKYBOX：'+$GameDirectory)
    if($Action-eq'Launch') {
        $null=Assert-SkyboxPayload $package
        if(!(Test-SkyboxRunning $GameDirectory)) {Start-Process -FilePath (Join-Path $GameDirectory 'SteamVR_SourceVRPlayer.exe') -WorkingDirectory $GameDirectory -WindowStyle Normal}
        exit 0
    }
    $needsAdmin=$Action-in@('LockUpdates','UnlockUpdates')
    if(!$needsAdmin) {
        foreach($directory in @($GameDirectory,(Join-Path $GameDirectory 'SteamVR_SourceVRPlayer_Data\Managed'))) {
            $probe=Join-Path $directory ('.skybox-write-probe-'+[guid]::NewGuid().ToString('N'))
            try {$stream=[IO.FileStream]::new($probe,[IO.FileMode]::CreateNew,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None,4096,[IO.FileOptions]::DeleteOnClose);$stream.Dispose()}
            catch [UnauthorizedAccessException] {$needsAdmin=$true;break}
        }
    }
    $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
    $principal=New-Object Security.Principal.WindowsPrincipal($identity)
    if($needsAdmin-and!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        if($Elevated) {throw '需要管理员权限才能写入此安装目录。'}
        Write-Host '写入安装目录或修改更新权限需要管理员权限，请在 Windows 提示中允许。'
        $arguments='-NoProfile -ExecutionPolicy Bypass -STA -File "'+$PSCommandPath+'" -Action '+$Action+' -GameDirectory "'+$GameDirectory+'" -Elevated'
        if($OriginalAssembly) {$arguments+=' -OriginalAssembly "'+[IO.Path]::GetFullPath($OriginalAssembly)+'"'}
        $process=Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -ArgumentList $arguments -Verb RunAs -Wait -PassThru
        exit $process.ExitCode
    }
    if($Action-eq'Install'-or$Action-eq'Uninstall') {
        $result=Invoke-SkyboxPatch -PackageRoot $package -GameDirectory $GameDirectory -Mode $Action -OriginalAssembly $OriginalAssembly
        switch($result.Status) {
            'Installed' {Write-Host '安装完成。打开 SKYBOX 即可使用全部增强功能。' -ForegroundColor Green}
            'AlreadyInstalled' {Write-Host '已经安装此版本，无需重复安装。' -ForegroundColor Green}
            'Uninstalled' {Write-Host '已恢复原版程序。个人设置和观看记录已保留。' -ForegroundColor Green}
            'AlreadyUninstalled' {Write-Host '当前已经是原版，无需重复卸载。'}
        }
        Write-Host ('本地备份目录：'+$result.BackupDirectory)
    } elseif($Action-eq'LockUpdates') {
        $null=Invoke-SkyboxUpdateLock -GameDirectory $GameDirectory
        Write-Host '已限制本应用的 Steam 更新。若 Steam 强制要求更新，可能提示磁盘写入错误；使用解除入口恢复。' -ForegroundColor Yellow
    } else {
        $null=Invoke-SkyboxUpdateLock -GameDirectory $GameDirectory -Unlock
        Write-Host '已恢复此工具保存的 Steam 更新目录权限和更新设置。' -ForegroundColor Green
    }
    if($Elevated) {$null=Read-Host '按回车关闭'}
    exit 0
} catch {
    Write-Host ('操作未完成：'+$_.Exception.Message) -ForegroundColor Red
    Write-Host '请完整解压 ZIP，退出 SKYBOX，并确认版本为 Steam Windows 1.1.8（2668）。'
    if($Elevated) {$null=Read-Host '按回车关闭'}
    exit 1
}
