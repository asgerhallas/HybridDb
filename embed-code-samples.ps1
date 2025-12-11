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

# Determine script root directory
$ScriptRoot = $PSScriptRoot
if (-not $ScriptRoot) {
    $ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}

# Make paths absolute relative to script location
if (-not [System.IO.Path]::IsPathRooted($DocsPath)) {
    $DocsPath = Join-Path $ScriptRoot $DocsPath
}
if (-not [System.IO.Path]::IsPathRooted($TestsPath)) {
    $TestsPath = Join-Path $ScriptRoot $TestsPath
}

function Get-CodeRegions {
    param([string]$filePath)
    
    $regions = @{}
    $content = Get-Content $filePath -Raw
    
    # Find all #region blocks
    $pattern = '#region\s+(\w+)\s*\r?\n(.*?)#endregion'
    $matches = [regex]::Matches($content, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    
    foreach ($match in $matches) {
        $regionName = $match.Groups[1].Value
        $code = $match.Groups[2].Value
        
        # Remove trailing whitespace but preserve leading structure
        $code = $code.TrimEnd()
        
        # Remove leading indentation
        $lines = $code -split '\r?\n'
        if ($lines.Count -gt 0) {
            # Find minimum indentation from non-empty lines
            $nonEmptyLines = $lines | Where-Object { $_.Trim() -ne '' }
            if ($nonEmptyLines.Count -gt 0) {
                $minIndent = ($nonEmptyLines | ForEach-Object { 
                    if ($_ -match '^(\s*)') { $Matches[1].Length } else { 0 }
                } | Measure-Object -Minimum).Minimum
                
                $code = ($lines | ForEach-Object {
                    if ($_.Trim() -ne '' -and $_.Length -ge $minIndent) {
                        $_.Substring($minIndent)
                    } elseif ($_.Trim() -eq '') {
                        ''  # Empty line
                    } else {
                        $_  # Line shorter than minIndent
                    }
                }) -join "`n"
            }
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

# Function to embed code regions into markdown files
function Update-MarkdownFile {
    param(
        [string]$markdownPath,
        [hashtable]$allRegions
    )
    
    if (-not (Test-Path $markdownPath)) {
        Write-Warning "Markdown file not found: $markdownPath"
        return 0
    }
    
    $content = Get-Content $markdownPath -Raw
    $originalContent = $content
    $script:updatedCount = 0
    
    # Pattern to match <!-- embed:TestFile#RegionName --> ... <!-- /embed -->
    $pattern = '<!--\s*embed:([^#\s]+)#([^\s]+)\s*-->.*?```.*?<!--\s*/embed\s*-->'
    $options = [System.Text.RegularExpressions.RegexOptions]::Singleline
    
    $content = [regex]::Replace($content, $pattern, {
        param($match)
        
        $testFile = $match.Groups[1].Value
        $regionName = $match.Groups[2].Value
        
        if ($allRegions.ContainsKey($testFile) -and $allRegions[$testFile].ContainsKey($regionName)) {
            $code = $allRegions[$testFile][$regionName]
            $script:updatedCount++
            
            # Return the replacement with the actual code
            "<!-- embed:$testFile#$regionName -->`n``````csharp`n$code`n```````n<!-- /embed -->"
        } else {
            Write-Warning "Region not found: $testFile#$regionName"
            $match.Value  # Return original if region not found
        }
    }, $options)
    
    # Only write if content changed
    if ($content -ne $originalContent) {
        Write-Host "  Updated $script:updatedCount code block(s)"
        Set-Content -Path $markdownPath -Value $content -NoNewline
        return $script:updatedCount
    }
    
    return 0
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

Write-Host ""
Write-Host "Total test files processed: $($allRegions.Count)"
Write-Host "Total code regions found: $(($allRegions.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum)"

# Update markdown files
Write-Host ""
Write-Host "Updating markdown files..."

$markdownFiles = Get-ChildItem -Path $DocsPath -Filter "*.md" -Recurse
$totalUpdates = 0

foreach ($mdFile in $markdownFiles) {
    Write-Host "Checking: $($mdFile.Name)"
    $updates = Update-MarkdownFile -markdownPath $mdFile.FullName -allRegions $allRegions
    $totalUpdates += $updates
}

Write-Host ""
Write-Host "Total code blocks updated: $totalUpdates"

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
