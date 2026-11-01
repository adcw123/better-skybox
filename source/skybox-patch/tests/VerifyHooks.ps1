param([string]$AssemblyPath = "$PSScriptRoot\..\backup\Assembly-CSharp.dll")
$ErrorActionPreference = 'Stop'
Add-Type -Path "$PSScriptRoot\..\tools\dnlib\lib\net45\dnlib.dll"
$module = [dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path -LiteralPath $AssemblyPath).Path)
$expected = @{
    'SkyboxVR.MainProcedure::Start' = @('Ensure')
    'SkyboxVR.DeleteFileScript::RefreshPanelState' = @('DeleteUi')
    'SkyboxVR.AdvancedPageManager::Start' = @('SettingsUi')
    'SkyboxVR.FileScrollView::FillVideoInfoToItem' = @('ReleaseCell','BindCell')
    'SkyboxVR.FileScrollView::FillPictureInfoToItem' = @('ReleaseCell','BindCell')
    'SkyboxVR.FileScrollView::FillDirectoryInfoToItem' = @('ReleaseCell','BindCell')
    'SkyboxVR.FileScrollView::Update' = @('ScratchKeys','ScratchVisible')
    'SkyboxVR.FileItemEvent::OnButtonClicked' = @('SkipFolderClick')
    'SkyboxVR.VideoTextureManager::LoadVideoFile' = @('BeforeVideo','AfterVideo')
    'SkyboxVR.BaseSliderControlScript::Awake' = @('EyeSlider')
    'SkyboxVR.VideoPlayerCtrl::HandleVideoLooping' = @('VideoEnded','EndHandling')
    'SkyboxVR.VideoFileManager::PlayNextVideoFile' = @('AutoNext')
    'SkyboxVR.VideoFileManager::set_CurrentDeleteFileState' = @('DeleteGate')
    'SkyboxVR.VideoPlayerCtrl::SaveVideoFile' = @('PreserveProgress')
    'SkyboxVR.FileScrollView::Awake' = @('BrowseUi')
    'SkyboxVR.FavoriteButtonScript::Awake' = @('FavoriteReady')
}
$failures = 0
$invalidScopes = @($module.GetTypeRefs() | Where-Object { $_.ResolutionScope -is [dnlib.DotNet.ModuleRef] -and $_.Namespace -eq 'SkyboxVR' })
if($invalidScopes.Count) { Write-Output "FAIL managed game types reference external netmodule: $($invalidScopes.FullName -join ', ')"; $failures++ }
else { Write-Output 'PASS managed game types reference their defining assembly' }
foreach ($key in $expected.Keys) {
    $typeName,$methodName = $key.Split(@('::'),[StringSplitOptions]::None)
    $method = $module.Find($typeName,$false).Methods | Where-Object Name -EQ $methodName
    foreach ($hook in $expected[$key]) {
        $calls = @($method.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'call' -and $_.Operand.DeclaringType.FullName -eq 'SkyboxUsability.Hooks' -and $_.Operand.Name -eq $hook })
        $count = if($key -eq 'SkyboxVR.DeleteFileScript::RefreshPanelState') {4} elseif($hook -eq 'EndHandling') {8} else {1}
        if ($calls.Count -eq $count) { Write-Output "PASS $key -> $hook ($count exits)" }
        else { Write-Output "FAIL $key -> $hook ($($calls.Count) calls)"; $failures++ }
    }
}
$save=$module.Find('SkyboxVR.VideoPlayerCtrl',$false).Methods | Where-Object Name -EQ SaveVideoFile
$saveIL=@($save.Body.Instructions)
$progressIndex=-1;$databaseIndex=-1
for($i=0;$i -lt $saveIL.Count;$i++){
    if($saveIL[$i].Operand -is [dnlib.DotNet.IMethod]){
        if($saveIL[$i].Operand.Name -eq 'PreserveProgress'){$progressIndex=$i}
        if($saveIL[$i].Operand.Name -eq 'UpdateVideoMetaByMediaTool'){$databaseIndex=$i}
    }
}
if($progressIndex -lt 0 -or $databaseIndex -le $progressIndex){$failures++;Write-Output 'FAIL accurate progress must run before DB save'}else{Write-Output 'PASS accurate progress runs before DB save'}
$filteredMethods=0
foreach($type in $module.GetTypes()){
    foreach($method in $type.Methods | Where-Object HasBody){
        $hooks=@($method.Body.Instructions | Where-Object {$_.Operand -is [dnlib.DotNet.IMethod] -and $_.Operand.DeclaringType.FullName -eq 'SkyboxUsability.Hooks' -and $_.Operand.Name -eq 'FilterList'})
        if(!$hooks.Count){continue}
        $returns=@($method.Body.Instructions | Where-Object {$_.OpCode.Name -eq 'ret'})
        if($hooks.Count -ne $returns.Count){$failures++;Write-Output "FAIL filter does not cover all returns: $($method.FullName)"}
        $filteredMethods++
    }
}
if($filteredMethods -lt 10){$failures++;Write-Output 'FAIL missing derived list mutation hooks'}else{Write-Output "PASS filter covers all exits of $filteredMethods list mutation methods"}
$update=$module.Find('SkyboxVR.FileScrollView',$false).Methods | Where-Object Name -EQ Update
$scratchAllocations=@($update.Body.Instructions | Where-Object {$_.OpCode.Name -eq 'newobj' -and $_.Operand.DeclaringType.FullName -in @('System.Collections.Generic.List`1<System.String>','System.Collections.Generic.HashSet`1<System.String>')})
if($scratchAllocations.Count){$failures++;Write-Output 'FAIL original per-frame scratch collections are still allocated'}else{Write-Output 'PASS original per-frame scratch collections are reused'}
$extensionPath=Join-Path (Split-Path (Resolve-Path -LiteralPath $AssemblyPath).Path) 'SkyboxUsability.dll'
if(Test-Path -LiteralPath $extensionPath){
    $extension=[dnlib.DotNet.ModuleDefMD]::Load($extensionPath)
    $refresh=$extension.Find('SkyboxUsability.CellTools',$false).Methods | Where-Object Name -EQ Refresh
    $polls=@($refresh.Body.Instructions | Where-Object {$_.Operand -is [dnlib.DotNet.IMethod] -and $_.Operand.DeclaringType.FullName -eq 'SkyboxUsability.CardDetails' -and $_.Operand.Name -eq 'Refresh'})
    if($polls.Count){$failures++;Write-Output 'FAIL metadata is still refreshed by cell polling'}else{Write-Output 'PASS metadata updates on binding instead of cell polling'}
}
if($failures) { throw "$failures hook contracts failed" }
