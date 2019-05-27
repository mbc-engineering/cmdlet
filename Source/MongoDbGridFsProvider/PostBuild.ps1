param (
	[string]$Targetpath,
	[string]$TargetDir
)

Write-Host "Get Assembly version $TargetPath"
$version = Get-Item $TargetPath | % versioninfo | % FileVersion

Write-Host "Update Module Manifest with Assembly version $version"
Update-ModuleManifest $TargetDir\MongoDbGridFsProvider.psd1 -ModuleVersion $version