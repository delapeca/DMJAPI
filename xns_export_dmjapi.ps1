# extract-controllers.ps1
# Extreu Program.cs i tots els Controllers actuals (sense .bak ni .backup)

param(
    [string]$Path = ".",
    [string]$Output = "api_controllers_dump.txt"
)

$separator = "`n" + ("=" * 80) + "`n"
$result = @()

# 1. Program.cs
$programCs = Join-Path $Path "Program.cs"
if (Test-Path $programCs) {
    $result += "${separator}FILE: Program.cs${separator}"
    $result += Get-Content $programCs -Raw
}

# 2. Tots els .cs dins Controllers (sense backups)
$controllers = Get-ChildItem -Path (Join-Path $Path "Controllers") -Recurse -Filter "*.cs" |
    Where-Object { $_.Name -notmatch '\.(bak|backup)_' } |
    Sort-Object FullName

foreach ($file in $controllers) {
    $relativePath = $file.FullName.Replace((Resolve-Path $Path).Path, "").TrimStart('\', '/')
    $result += "${separator}FILE: $relativePath${separator}"
    $result += Get-Content $file.FullName -Raw
}

$result | Out-File -FilePath $Output -Encoding UTF8
$fileCount = $controllers.Count + 1
$lineCount = ($result | Measure-Object -Line).Lines
Write-Host "✅ Exportats $fileCount fitxers ($lineCount línies) a: $Output" -ForegroundColor Green

# ./xns_export_dmjapi.ps1
