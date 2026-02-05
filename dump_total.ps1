<# 
    Exportar codi d'una API C# a UN SOL ARXIU TXT amb llistes d'INCLÒS i EXCLOS
    Provat amb PowerShell 5.1
#>

# Carpeta arrel del projecte API
$SourceRoot = "E:\Programacio\C#\XNApiRest"

# Arxiu TXT on es guardarà tot
$OutputFile = "E:\XNApiRest_Codi_Total.txt"

# Llista d'elements que vull EXPORTAR (rutes relatives o patrons)
$IncludeItems = @(
    "App_Data",
    "Controllers",
    "Functions", 
    "Infrastructure",
    "Loxone",
    "Models",
    "QRMulti",
    "Querys",
    "Services",
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

# Funció auxiliar: comprovar si una ruta relativa coincideix amb alguna entrada d'una llista de patrons
function Test-MatchPattern {
    param([string] $RelativePath, [string[]] $Patterns)
    foreach ($p in $Patterns) {
        if ($RelativePath -like $p -or (Split-Path $RelativePath -Leaf) -like $p) {
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

# Obtenir carpetes amb profunditat i ordenar per ruta
$treeFolders = Get-ChildItem -Path $SourceRoot -Recurse -Directory -Force | 
    Sort-Object FullName | 
    Where-Object { 
        $relPath = $_.FullName.Substring($SourceRoot.Length).TrimStart('\')
        -not (Test-MatchPattern -RelativePath $relPath -Patterns $ExcludeItems) -and
        (Test-MatchPattern -RelativePath $relPath -Patterns $IncludeItems)
    }

foreach ($folder in $treeFolders) {
    $relPath = $folder.FullName.Substring($SourceRoot.Length).TrimStart('\')
    $depth = ($relPath.Split('\').Count - 1)
    $indent = '  ' * $depth
    "  📁 $indent$($relPath.Split('\')[-1])" | Out-File -FilePath $OutputFile -Append -Encoding UTF8
}

"" | Out-File -FilePath $OutputFile -Append -Encoding UTF8



# Obtenir tots els elements
$items = Get-ChildItem -Path $SourceRoot -Recurse -Force

foreach ($item in $items) {
    $relativePath = $item.FullName.Substring($SourceRoot.Length).TrimStart('\')

    # 1) Comprovar EXCLUSIÓ
    if (Test-MatchPattern -RelativePath $relativePath -Patterns $ExcludeItems) { continue }
    
    # 2) Comprovar INCLUSIÓ  
    if (-not (Test-MatchPattern -RelativePath $relativePath -Patterns $IncludeItems)) { continue }

    if ($item.PSIsContainer) {
        # ESTRUCTURA DE CARPETES
        "📁 CARPETA: $relativePath" | Out-File -FilePath $OutputFile -Append -Encoding UTF8
    } else {
        # FITXER - Afegir contingut
        "`n" + "="*80 | Out-File -FilePath $OutputFile -Append -Encoding UTF8
        "📄 FITXER: $relativePath" | Out-File -FilePath $OutputFile -Append -Encoding UTF8
        "="*80 | Out-File -FilePath $OutputFile -Append -Encoding UTF8
        
        try {
            $content = Get-Content -Path $item.FullName -Raw -Encoding UTF8
            $lines = $content -split "`n"
            $filesProcessed++
            $linesTotal += $lines.Count
            
            $content | Out-File -FilePath $OutputFile -Append -Encoding UTF8
        }
        catch {
            "❌ ERROR llegint $($item.FullName): $($_.Exception.Message)" | Out-File -FilePath $OutputFile -Append -Encoding UTF8
        }
    }
}

# MISSATGE FINAL
Write-Host "`n✅ EXPORTACIÓ COMPLETADA!" -ForegroundColor Green
Write-Host "📄 Arxiu generat: $OutputFile" -ForegroundColor Yellow
Write-Host "📊 $filesProcessed fitxers processats, $linesTotal línies totals" -ForegroundColor White
Write-Host "`nObre '$OutputFile' (Notepad++ o VS Code) per veure tot el teu codi!" -ForegroundColor Green
Write-Host "Tamañ: $((Get-Item $OutputFile).Length / 1KB) KB" -ForegroundColor Cyan
