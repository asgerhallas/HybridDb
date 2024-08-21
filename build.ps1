param (
    [Parameter(Mandatory=$true, ValueFromPipeline=$true)][string]$Version
)

Import-Module .\functions.ps1 -Force

Run "Build" { dotnet build -p:Version=$Version } 
Run "Test" { dotnet test --no-build --logger:"console;verbosity=normal" }
Run "Pack" { dotnet pack src/HybridDb/ -c Release --include-symbols -p:SymbolPackageFormat=snupkg -p:Version=$Version }