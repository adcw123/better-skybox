$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$compiler=Join-Path $root 'tools\roslyn\tasks\net472\csc.exe'
$suites=@(
    @{Name='PolicyTests';Sources=@('Policy.cs')},
    @{Name='LibraryTests';Sources=@('LibraryPolicy.cs')},
    @{Name='WorkflowTests';Sources=@('Policy.cs','WorkflowPolicy.cs','ScopedDefaults.cs')},
    @{Name='PerformanceTests';Sources=@('PerformancePolicy.cs')}
)
foreach($suite in $suites){
    $target=Join-Path $root ('build\'+$suite.Name+'.exe')
    $sources=@(Join-Path $PSScriptRoot ($suite.Name+'.cs'))+@($suite.Sources | ForEach-Object {Join-Path $root ('src\'+$_)})
    & $compiler /nologo "/out:$target" @sources
    if($LASTEXITCODE){throw "Compilation failed: $($suite.Name)"}
    & $target
    if($LASTEXITCODE){throw "Tests failed: $($suite.Name)"}
}
