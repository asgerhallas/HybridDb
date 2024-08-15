param (
    [Parameter(Mandatory=$true)][string]$NugetApiKey
    #[Parameter(Mandatory=$true)][string]$Description
)

Import-Module .\functions.ps1 -Force

# Run "Nuget" { dotnet nuget push **\*.nupkg --api-key $NugetApiKey --source https://api.nuget.org/v3/index.json } 
Run "Github" {
    $body = [System.IO.File]::ReadAllBytes($fullpath)
}