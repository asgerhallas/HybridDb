[CmdLetbinding()]
param (
	[Parameter(Mandatory)][string]$Version,
	[Parameter(Mandatory)][string]$NugetApiKey
)

$ErrorActionPreference = "Stop";

dotnet restore --verbosity n
dotnet build --verbosity n --no-restore -p:Version=$Version
#dotnet test --no-build

if (!$NugetApiKey) {
	Write-Host "Not safe to pack nugets from external pull request.";
	exit(0);
}

dotnet pack src/HybridDb/ -c Release --verbosity n --no-build --include-symbols -p:SymbolPackageFormat=snupkg -p:Version=$Version