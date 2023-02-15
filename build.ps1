[CmdLetbinding()]
param (
	[Parameter(Mandatory)][string]$Version,
	[Parameter(Mandatory)][string]$NugetApiKey
)

$ErrorActionPreference = "Stop";

Write-Host "Build"
dotnet build --verbosity n -p:Version=$Version

Write-Host "Test"
#dotnet test --no-build

if (!$NugetApiKey) {
	Write-Host "Not safe to pack nugets from external pull request.";
	exit(0);
}

Write-Host "Pack Nuget"
dotnet pack src/HybridDb/ -c Release --verbosity n --no-build --include-symbols -p:SymbolPackageFormat=snupkg -p:Version=$Version