# Post-build script to fix VS Code inline markdown highlighting
# Adds a blank line after anchor tags so they don't interfere with code fence detection

Get-ChildItem -Path "$PSScriptRoot\docs\*.md" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    
    # Add a newline after anchor tags - pattern matches the anchor followed immediately by code fence
    $pattern = "(<a id='snippet-[^']+'>)</a>\r?\n" + '```'
    $replacement = '$1</a>' + "`r`n`r`n" + '```'
    $content = $content -replace $pattern, $replacement
    
    Set-Content -Path $_.FullName -Value $content -NoNewline
}

Write-Host "Fixed snippet anchor tags in documentation files" -ForegroundColor Green
