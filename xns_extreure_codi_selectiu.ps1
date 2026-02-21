# extract-files.ps1
# Extreu el contingut dels fitxers especificats a $files

param(
    [string]$Path = ".",
    [string]$Output = "code_export.txt"
)

# ════════════════════════════════════════════════════════════
# MODIFICA AQUESTA LLISTA CADA COP QUE CLAUDE ET DEMANI CODI
# ════════════════════════════════════════════════════════════
$files = @(
    "Controllers/ItemsController.cs"
    "Infrastructure/ApiKeys/ApiKeyProfileMiddleware.cs"
    "Infrastructure/ApiKeys/ApiKeyProfilesOptions.cs"
    "Controllers/SAPLoginController.cs"
    "Services/SAPLoginService.cs"
    "Functions/Login.cs"
    "Functions/Encryption.cs"
    "Functions/Dades.cs"
    )

$separator = "`n" + ("=" * 80) + "`n"
$result = @()
$found = 0
$notFound = @()

foreach ($file in $files) {
    $fullPath = Join-Path $Path $file
    if (Test-Path $fullPath) {
        $result += "${separator}FILE: $file${separator}"
        $result += Get-Content $fullPath -Raw
        $found++
    } else {
        $notFound += $file
    }
}

$result | Out-File -FilePath $Output -Encoding UTF8

Write-Host "`n✅ Exportats $found/$($files.Count) fitxers a: $Output" -ForegroundColor Green

if ($notFound.Count -gt 0) {
    Write-Host "❌ No trobats:" -ForegroundColor Red
    $notFound | ForEach-Object { Write-Host "   - $_" -ForegroundColor Yellow }
}

# ./xns_extreure_codi_selectiu.ps1
