# Script to find files with actual content changes (ignoring whitespace/mode)
# Run this from within the pokeemerald directory

$allModified = git status --short | ForEach-Object { 
    $line = $_.ToString().Trim()
    if ($line -match '^([^ ]+) (.+)$') {
        [PSCustomObject]@{
            Status = $matches[1]
            File = $matches[2]
        }
    }
}

Write-Host "Checking for files with actual content changes (ignoring whitespace)..." -ForegroundColor Cyan
Write-Host ""

$filesWithRealChanges = @()

foreach ($item in $allModified) {
    $file = $item.File
    
    # Skip .gitignore files as they're usually just line ending changes
    if ($file -like "*.gitignore") {
        continue
    }
    
    # Check if file is binary
    $isBinary = git diff --numstat HEAD -- "$file" | ForEach-Object {
        if ($_ -match '^-') { $true } else { $false }
    }
    
    if ($isBinary) {
        # For binary files, check if there's an actual size/content difference
        $diff = git diff --numstat HEAD -- "$file"
        if ($diff -and $diff -notmatch '^0\s+0\s+') {
            $filesWithRealChanges += $file
        }
    } else {
        # For text files, check if there are content changes ignoring whitespace
        $diff = git diff --ignore-all-space --ignore-blank-lines --ignore-space-at-eol HEAD -- "$file"
        if ($diff -and $diff.Length -gt 0) {
            # Check if diff has actual content (not just mode changes)
            $hasContent = $false
            foreach ($line in $diff) {
                if ($line -match '^[\+\-]' -and $line -notmatch '^[\+\-]\s*$') {
                    $hasContent = $true
                    break
                }
            }
            if ($hasContent) {
                $filesWithRealChanges += $file
            }
        }
    }
}

Write-Host "Files with actual content changes:" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Green
Write-Host ""

if ($filesWithRealChanges.Count -eq 0) {
    Write-Host "No files with actual content changes found." -ForegroundColor Yellow
} else {
    foreach ($file in $filesWithRealChanges) {
        Write-Host $file
    }
    Write-Host ""
    Write-Host "Total: $($filesWithRealChanges.Count) file(s)" -ForegroundColor Cyan
}

# Filter to show only map files
Write-Host ""
Write-Host "Map files with actual content changes:" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""

$mapFiles = $filesWithRealChanges | Where-Object { $_ -like "*data/maps/*" -and $_ -notlike "*.gitignore" }

if ($mapFiles.Count -eq 0) {
    Write-Host "No map files with actual content changes found." -ForegroundColor Yellow
} else {
    foreach ($file in $mapFiles) {
        Write-Host $file
    }
    Write-Host ""
    Write-Host "Total: $($mapFiles.Count) map file(s)" -ForegroundColor Cyan
}

