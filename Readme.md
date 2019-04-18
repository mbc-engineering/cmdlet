# PowerShell Cmdlets/Providers .NET

## Documentation
For documentation go to [Documentation of this repository](Docs/index.md)

## How to build and run the project
### Requirements
- Access to internet for nuget feeds.
- .Net Framework >= 4.7.2

## How to use:
### Import module in PS:

> Import-Module .\MongoDbGridFsProvider.dll

### Register PSDrive:
> New-PSDrive -Name Mongo -PSProvider MongoDb -Root '' -Host 'localhost' -Database 'files' -Collection ''

### Commands:
- Get-Item Mongo:\21312349187246198 -Target C:\Temp\A.foo
- Set-Item Mongo:C:\Temp\B.foo
- Remove-Item Mongo:\123769231768231876
- Rename-Item Mongo:\93097432780089734 -NewName Fail-Test.backup


