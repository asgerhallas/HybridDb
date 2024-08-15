$ErrorActionPreference = "Stop";

function Run {
    param (
        [Parameter(Mandatory=$true)][string]$BlockName,
        [Parameter(Mandatory=$true)][scriptblock]$ScriptBlock
    )
    
    & @ScriptBlock

    if (($lastexitcode -ne 0)) {
        Write-Host $("'" + $BlockName + "' failed: " + $lastexitcode)
        exit $lastexitcode
    }
}