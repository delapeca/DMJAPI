<# 
    Exportar codi d'una API C# a UN SOL ARXIU TXT amb llistes d'INCLÒS i EXCLOS
    Versió CORREGIDA per fitxers concrets
    Provat amb PowerShell 5.1
#>

# Carpeta arrel del projecte API
$SourceRoot = "E:\Programacio\C#\XNApiRest"

# Arxiu TXT on es guardarà tot
$OutputFile = "E:\XNApiRest_dump_selectiu.txt"

# Llista d'elements que vull EXPORTAR (rutes relatives COMPLETES o patrons)
$IncludeItems = @(
    "Controllers/DbSetupController.cs",
    "Controllers/SapSqlSchemaController.cs",
    "Services/SqlSchemaService.cs", 
    "Models/SqlSchemaSpec.cs",
    "Functions/Dades.cs",
    "Services/SAPLoginService.cs",
    "Functions/DataAccess.cs",
    "Program.cs",
    "appsettings*.json"
)

# Llista d'elements que NO vull EXPORTAR (rutes relatives o patrons)
$ExcludeItems = @(
    "bin",
    "obj",
    ".git",
    ".vs",
    "*Test*",
    "*.bak_",
    "*.ps1"
)

# FUNCIÓ CORREGIDA: ara funciona amb rutes completes com "Controllers/file.cs"
function Test-MatchPattern {
    param([string] $RelativePath, [string[]] $Patterns)
    
    # Normalitzar separadors per si de cas
    $normalizedPath = $RelativePath -replace '\\', '/'
    
    foreach ($p in $Patterns) {
        $normalizedPattern = $p -replace '\\', '/'
        
        # Comprovar coincidència exacta o patró
        if ($normalizedPath -like $normalizedPattern -or 
            (Split-Path $RelativePath -Leaf) -like $normalizedPattern -or
            $normalizedPath -eq $normalizedPattern) {
            return $true
        }
    }
    return $false
}

# Comptadors
$filesProcessed = 0
$linesTotal = 0

# Crear arxiu TXT (sobreescriure)
"=== EXPORTACIÓ DE CÒDIG SELECCIONAT ===" | Out-File -FilePath $OutputFile -Encoding UTF8
"Data: $(Get-Date)" | Out-File -FilePath $OutputFile -Append -Encoding UTF8
"Origen: $SourceRoot" | Out-File -FilePath $OutputFile -Append -Encoding UTF8
"" | Out-File -FilePath $OutputFile -Append -Encoding UTF8

# GENERAR TREE ORDENAT DE L'ESTRUCTURA SELECCIONADA
"=== ESTRUCTURA DE CARPETES (TREE ORDENAT) ===" | Out-File -FilePath $OutputFile -Append -Encoding UTF8

# Obtenir carpetes amb profunditat i ordenar per ruta (CORREGIT TrimStart('\'))
$treeFolders = Get-ChildItem -Path $SourceRoot -Recurse -Directory -Force | 
    Sort-Object FullName | 
    Where-Object { 
        $relPath = $_.FullName.Substring($SourceRoot.Length).TrimStart('\')
        -not (Test-MatchPattern -RelativePath $relPath -Patterns $ExcludeItems)
    }

foreach ($folder in $treeFolders) {
    $relPath = $folder.FullName.Substring($SourceRoot.Length).TrimStart('\')
    $depth = ($relPath.Split('\').Count - 1)
    $indent = '  ' * $depth
    "  📁 $indent$($relPath.Split('\')[-1])" | Out-File -FilePath $OutputFile -Append -Encoding UTF8
}

"" | Out-File -FilePath $OutputFile -Append -Encoding UTF8

# Obtenir només FITXERS (no carpetes per al contingut)
$files = Get-ChildItem -Path $SourceRoot -Recurse -File -Force

foreach ($file in $files) {
    $relativePath = $file.FullName.Substring($SourceRoot.Length).TrimStart('\')

    # 1) Comprovar EXCLUSIÓ
    if (Test-MatchPattern -RelativePath $relativePath -Patterns $ExcludeItems) { continue }
    
    # 2) Comprovar INCLUSIÓ  
    if (-not (Test-MatchPattern -RelativePath $relativePath -Patterns $IncludeItems)) { continue }

    # FITXER - Afegir contingut
    "`n" + "="*80 | Out-File -FilePath $OutputFile -Append -Encoding UTF8
    "📄 FITXER: $relativePath" | Out-File -FilePath $OutputFile -Append -Encoding UTF8
    "="*80 | Out-File -FilePath $OutputFile -Append -Encoding UTF8
    
    try {
        $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
        $lines = $content -split "`n"
        $filesProcessed++
        $linesTotal += $lines.Count
        
        $content | Out-File -FilePath $OutputFile -Append -Encoding UTF8
        "" | Out-File -FilePath $OutputFile -Append -Encoding UTF8
    }
    catch {
        "❌ ERROR llegint $($file.FullName): $($_.Exception.Message)" | Out-File -FilePath $OutputFile -Append -Encoding UTF8
    }
}

# MISSATGE FINAL
Write-Host "`n✅ EXPORTACIÓ COMPLETADA!" -ForegroundColor Green
Write-Host "📄 Arxiu generat: $OutputFile" -ForegroundColor Yellow
Write-Host "📊 $filesProcessed fitxers processats, $linesTotal línies totals" -ForegroundColor White
Write-Host "Tamañ: $([math]::Round((Get-Item $OutputFile).Length / 1KB, 1)) KB" -ForegroundColor Cyan


# Executar amb: ./dump_selectiu.ps1 