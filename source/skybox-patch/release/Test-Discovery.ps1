param([Parameter(Mandatory=$true)][string]$PackageRoot,[string]$ExpectedInstallation)
$ErrorActionPreference='Stop'
. (Join-Path $PackageRoot 'tools\ReleaseCore.ps1')
$found=@(Find-SkyboxDirectories)
$unique=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($path in $found){if(!$unique.Add($path)){throw 'Duplicate installation from registry/path case differences'}}
if($ExpectedInstallation-and$ExpectedInstallation-notin$found){throw 'Expected Steam installation was not discovered'}
'PASS Steam library discovery ignores missing libraries and deduplicates installations'
