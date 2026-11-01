param([string]$GameDirectory = 'C:\Program Files (x86)\Steam\steamapps\common\SKYBOX VR Video Player')
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GameDirectory 'SteamVR_SourceVRPlayer_Data\Managed'
$compiler = Join-Path $PSScriptRoot 'tools\roslyn\tasks\net472\csc.exe'
$build = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Force -Path $build | Out-Null
$references = @('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll','CoroutineManager.dll','UnityEngine.CoreModule.dll','UnityEngine.UI.dll','UnityEngine.UIModule.dll','UnityEngine.InputLegacyModule.dll','UnityEngine.TextRenderingModule.dll') | ForEach-Object { '/r:' + (Join-Path $managed $_) }
$references += '/r:' + (Join-Path $PSScriptRoot 'backup\Assembly-CSharp.dll')
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
& $compiler /nologo /nostdlib+ /target:library /optimize+ /out:"$build\SkyboxUsability.dll" @references @sources
if($LASTEXITCODE) { throw 'Extension compilation failed' }
Copy-Item -LiteralPath "$PSScriptRoot\tools\dnlib\lib\net45\dnlib.dll" -Destination "$build\dnlib.dll" -Force
& $compiler /nologo /optimize+ /r:"$build\dnlib.dll" /out:"$build\PatchAssembly.exe" "$PSScriptRoot\PatchAssembly.cs"
if($LASTEXITCODE) { throw 'Patcher compilation failed' }
& "$build\PatchAssembly.exe" "$PSScriptRoot\backup\Assembly-CSharp.dll" "$build\SkyboxUsability.dll" "$build\Assembly-CSharp.dll"
if($LASTEXITCODE) { throw 'Patching failed' }
