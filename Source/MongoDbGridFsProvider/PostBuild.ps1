param (
	[string]$Targetpath,
	[string]$TargetDir,
	[string]$TargetName
)

<#
This script updated the Powershell Module Manifest to the actual module assembly version.
#>

Write-Host "Get Assembly version $TargetPath"
$version = Get-Item $TargetPath | % versioninfo | % FileVersion

$manifestPath = "$TargetDir$TargetName.psd1"
Write-Host "Update Module Manifest '$manifestPath' with Assembly version $version"
Update-ModuleManifest $manifestPath -ModuleVersion $version