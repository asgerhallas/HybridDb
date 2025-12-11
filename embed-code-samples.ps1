#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Embeds code snippets from test files into documentation markdown files.

.DESCRIPTION
    This script extracts code regions marked with #region/#endregion comments from test files
    and embeds them into markdown files, replacing existing code blocks with references.

.PARAMETER DocsPath
    Path to the docs directory containing markdown files

.PARAMETER TestsPath
    Path to the test files containing code regions

.EXAMPLE
    .\embed-code-samples.ps1 -DocsPath "docs" -TestsPath "src\HybridDb.Tests\Documentation"
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$DocsPath = "docs",
    
    [Parameter(Mandatory=$false)]
    [string]$TestsPath = "src\HybridDb.Tests\Documentation"
)

function Get-CodeRegions {
    param([string]$filePath)
    
    $regions = @{}
    $content = Get-Content $filePath -Raw
    
    # Find all #region blocks
    $pattern = '#region\s+(\w+)\s*\r?\n(.*?)#endregion'
    $matches = [regex]::Matches($content, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    
    foreach ($match in $matches) {
        $regionName = $match.Groups[1].Value
        $code = $match.Groups[2].Value.Trim()
        
        # Remove leading indentation
        $lines = $code -split '\r?\n'
        if ($lines.Count -gt 0) {
            $minIndent = ($lines | Where-Object { $_.Trim() } | ForEach-Object { 
                if ($_ -match '^(\s*)(.*)$') { $Matches[1].Length } else { 0 }
            } | Measure-Object -Minimum).Minimum
            
            $code = ($lines | ForEach-Object {
                if ($_.Length -ge $minIndent) {
                    $_.Substring($minIndent)
                } else {
                    $_
                }
            }) -join "`n"
        }
        
        $regions[$regionName] = $code
    }
    
    return $regions
}

function Update-MarkdownWithCodeRegions {
    param(
        [string]$markdownPath,
        [hashtable]$allRegions
    )
    
    $content = Get-Content $markdownPath -Raw
    
    # Replace code blocks with embedded regions
    # Format: ```csharp
    #         [//]: # (embed:TestFile#RegionName)
    #         ... code ...
    #         ```
    
    # For now, let's create a mapping file
    $mappingFile = $markdownPath -replace '\.md$', '.code-mapping.json'
    
    $mapping = @{}
    foreach ($testFile in $allRegions.Keys) {
        foreach ($region in $allRegions[$testFile].Keys) {
            $mapping["$testFile#$region"] = $allRegions[$testFile][$region]
        }
    }
    
    $mapping | ConvertTo-Json -Depth 10 | Set-Content $mappingFile
    Write-Host "Created mapping file: $mappingFile"
}

# Main execution
Write-Host "Extracting code regions from test files..."

$testFiles = Get-ChildItem -Path $TestsPath -Filter "Doc*.cs" -Recurse
$allRegions = @{}

foreach ($testFile in $testFiles) {
    Write-Host "Processing: $($testFile.Name)"
    $regions = Get-CodeRegions -filePath $testFile.FullName
    
    if ($regions.Count -gt 0) {
        $allRegions[$testFile.BaseName] = $regions
        Write-Host "  Found $($regions.Count) regions"
        
        foreach ($regionName in $regions.Keys) {
            Write-Host "    - $regionName"
        }
    }
}

Write-Host "`nTotal test files processed: $($allRegions.Count)"
Write-Host "Total code regions found: $(($allRegions.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum)"

# Output region index
Write-Host "`n=== Code Region Index ==="
foreach ($testFile in $allRegions.Keys | Sort-Object) {
    Write-Host "`n$testFile`:"
    foreach ($region in $allRegions[$testFile].Keys | Sort-Object) {
        Write-Host "  - $region"
    }
}
