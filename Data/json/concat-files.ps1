
<#
  concat-files.ps1 (ASCII-only, PS 5.1+ compatible)
  - Recursively collects files by extensions (default .cs, .json)
  - Scans the folder where this script (.ps1) lives, by default
  - Outputs UTF-8 (BOM) text with Markdown fences
  - Options:
      -Output <name>   : default = code_bundle.txt
      -Timestamp       : when present, add _YYYYMMDD_HHMMSS before extension
      -Append          : append to output instead of overwrite
      -Root <path>     : scan this root instead of script folder (optional)
#>

param(
    # Scan root. If not provided, use the .ps1's directory.
    [string] $Root,

    # Target extensions (normalized to dot-leading). Default: .cs, .json
    [string[]] $Extensions = @('.cs', '.json'),

    # Output file name (default fixed name)
    [string] $Output = 'code_bundle.txt',

    # Add timestamp suffix (e.g., code_bundle_20260116_112233.txt)
    [switch] $Timestamp,

    # Append instead of overwrite
    [switch] $Append
)

$ErrorActionPreference = 'Stop'

# ----- Resolve scan root -----
# Default to the directory where this script resides
if ([string]::IsNullOrWhiteSpace($Root)) {
    if ($PSCommandPath) {
        $Root = Split-Path -Parent $PSCommandPath
    } else {
        # Fallback: current working directory
        $Root = (Get-Location).Path
    }
}
if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
    Write-Host ('Scan root not found: ' + $Root) -ForegroundColor Yellow
    exit 1
}
$baseDir = (Convert-Path -LiteralPath $Root)

# ----- Normalize extensions (.cs -> .cs / cs -> .cs) -----
if ($null -eq $Extensions -or $Extensions.Count -eq 0) {
    $Extensions = @('.cs', '.json')
} else {
    $norm = @()
    foreach ($ex in $Extensions) {
        if ([string]::IsNullOrWhiteSpace($ex)) { continue }
        if ($ex -match '^\.') { $norm += $ex } else { $norm += ('.' + $ex) }
    }
    if ($norm.Count -eq 0) { $norm = @('.cs', '.json') }
    $Extensions = $norm
}

# ----- Create a HashSet[string] to collect unique files (case-insensitive) -----
$allFiles = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

function Add-FilesRecursively([string] $rootPath) {
    foreach ($ext in $Extensions) {
        # Recurse and gather files of each extension
        Get-ChildItem -LiteralPath $rootPath -Recurse -File -Filter "*$ext" -ErrorAction SilentlyContinue |
            ForEach-Object { [void]$allFiles.Add($_.FullName) }
    }
}

Add-FilesRecursively -rootPath $baseDir

if ($allFiles.Count -eq 0) {
    Write-Host ('No target files found (extensions: ' + ($Extensions -join ', ') + ').') -ForegroundColor Yellow
    exit 0
}

# ----- Output path (fixed name by default; optional timestamp) -----
function Ensure-OutputPath([string]$outName, [bool]$useTimestamp) {
    if ([string]::IsNullOrWhiteSpace($outName)) { $outName = 'code_bundle.txt' }
    if ($useTimestamp) {
        $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
        $base  = [System.IO.Path]::GetFileNameWithoutExtension($outName)
        $ext   = [System.IO.Path]::GetExtension($outName)
        if ([string]::IsNullOrEmpty($ext)) { $ext = '.txt' }
        $outName = $base + '_' + $stamp + $ext
    }
    try {
        $p = Join-Path -Path $baseDir -ChildPath $outName
        return (Convert-Path $p -ErrorAction Stop)
    } catch {
        return (Join-Path -Path $baseDir -ChildPath $outName)
    }
}

$OutputPath = Ensure-OutputPath -outName $Output -useTimestamp:$Timestamp.IsPresent

# ----- Helper: choose Markdown fence label by extension -----
function Get-FenceLabel([string] $filePath) {
    $e = [System.IO.Path]::GetExtension($filePath).ToLowerInvariant()
    switch ($e) {
        '.cs'   { return '```csharp' }
        '.json' { return '```json' }
        default { return '```' }
    }
}

# ----- Write header (overwrite or append) -----
if ($Append) {
    Add-Content -LiteralPath $OutputPath -Encoding utf8 -Value '=== Code Bundle ==='
    Add-Content -LiteralPath $OutputPath -Encoding utf8 -Value ('Generated at: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
    Add-Content -LiteralPath $OutputPath -Encoding utf8 -Value ('Root: ' + $baseDir)
    Add-Content -LiteralPath $OutputPath -Encoding utf8 -Value ('Extensions: ' + ($Extensions -join ', '))
    Add-Content -LiteralPath $OutputPath -Encoding utf8 -Value ('Count: ' + $allFiles.Count)
    Add-Content -LiteralPath $OutputPath -Encoding utf8 -Value ''
} else {
    '=== Code Bundle ===' | Set-Content -LiteralPath $OutputPath -Encoding utf8
    ('Generated at: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')) | Add-Content -LiteralPath $OutputPath -Encoding utf8
    ('Root: ' + $baseDir) | Add-Content -LiteralPath $OutputPath -Encoding utf8
    ('Extensions: ' + ($Extensions -join ', ')) | Add-Content -LiteralPath $OutputPath -Encoding utf8
    ('Count: ' + $allFiles.Count) | Add-Content -LiteralPath $OutputPath -Encoding utf8
    '' | Add-Content -LiteralPath $OutputPath -Encoding utf8
}

# ----- Relative path base (for pretty section headers) -----
$uriBase = New-Object System.Uri($baseDir + '\')

$sorted = $allFiles | Sort-Object
foreach ($f in $sorted) {
    $rel = $f
    try {
        $uriFile = New-Object System.Uri($f)
        $rel = $uriBase.MakeRelativeUri($uriFile).ToString() -replace '/', '\'
    } catch { }

    ('=== ' + $rel + ' ===') | Add-Content -LiteralPath $OutputPath -Encoding utf8

    $fence = Get-FenceLabel -filePath $f
    $fence | Add-Content -LiteralPath $OutputPath -Encoding utf8

    Get-Content -LiteralPath $f -Raw -Encoding UTF8 | Add-Content -LiteralPath $OutputPath -Encoding utf8

    '```' | Add-Content -LiteralPath $OutputPath -Encoding utf8
    ''    | Add-Content -LiteralPath $OutputPath -Encoding utf8
}

Write-Host ('Done: ' + $OutputPath) -ForegroundColor Green
