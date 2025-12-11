$filePath = "src\HybridDb.Tests\Documentation\Doc01_GettingStartedTests.cs"
$content = Get-Content $filePath -Raw

# Find all #region blocks
$pattern = '#region\s+QuickStart_BasicExample\s*\r?\n(.*?)#endregion'
$match = [regex]::Match($content, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)

if ($match.Success) {
    $regionName = "QuickStart_BasicExample"
    $rawCode = $match.Groups[1].Value
    Write-Host "=== RAW CAPTURED (before .Trim()) ==="
    Write-Host "Length: $($rawCode.Length)"
    Write-Host "First 100 chars: [$($rawCode.Substring(0, [Math]::Min(100, $rawCode.Length)))]"
    
    $code = $rawCode.Trim()
    
    Write-Host "`n=== After .Trim() ==="
    $lines = $code -split '\r?\n'
    Write-Host "Total lines: $($lines.Count)"
    Write-Host "First 3 lines:"
    $lines[0..2] | ForEach-Object { Write-Host "[$_]" }
    
    # Remove leading indentation
    if ($lines.Count -gt 0) {
        # Find minimum indentation from non-empty lines
        $nonEmptyLines = $lines | Where-Object { $_.Trim() -ne '' }
        Write-Host "`nNon-empty lines: $($nonEmptyLines.Count)"
        
        if ($nonEmptyLines.Count -gt 0) {
            $indents = $nonEmptyLines | ForEach-Object { 
                if ($_ -match '^(\s*)') { $Matches[1].Length } else { 0 }
            }
            Write-Host "Indentations: $($indents -join ', ')"
            
            $minIndent = ($indents | Measure-Object -Minimum).Minimum
            Write-Host "Minimum indent: $minIndent"
            
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
    
    Write-Host "`n=== After indentation removal ==="
    $newLines = $code -split '\r?\n'
    Write-Host "First 3 lines:"
    $newLines[0..2] | ForEach-Object { Write-Host "[$_]" }
}
