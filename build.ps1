[CmdLetbinding()]
param (
	[Parameter(Mandatory)][string]$Version,
	[switch]$IsForeignPullRequest = $false
)

$ErrorActionPreference = "Stop";

dotnet build -p:Version=$Version
#dotnet test --no-build

if ($IsForeignPullRequest) {
	Write-Host "Not safe";
	exit(0);
}

dotnet pack src/HybridDb/ -c Release --no-build --include-symbols -p:SymbolPackageFormat=snupkg -p:Version=$Version

