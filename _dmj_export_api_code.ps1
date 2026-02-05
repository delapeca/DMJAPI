param()

Write-Host "=== DOMENJÓ · Export snapshot API .NET (C#) ==="

###############################################################################
# 1) LOCALITZAR ARREL DEL PROJECTE
###############################################################################
$ProjectRoot = Get-Location
Write-Host "[1/6] Arrel del projecte API: $ProjectRoot"

###############################################################################
# 2) DEFINIR FITXER DE SORTIDA
###############################################################################
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$outFile = Join-Path $ProjectRoot "_dump_c#_dmjapi_$ts.txt"

Write-Host "[2/6] Fitxer de sortida: $outFile"
"" | Out-File -FilePath $outFile -Encoding UTF8  # crear/buidar

###############################################################################
# 3) DIRECTORIS I FITXERS A INCLÒURE
###############################################################################
$dirCandidates = @(
    "App_Data"
    "Properties"
    "wwwroot"
    "Controllers"
    "Funcions"
    "Functions"
    "Infrastructure"
    "Loxone"
    "Models"
    "QrMulti"
    "Querys"
    "Queries"
    "Services"
    "Properties"
)

$rootFileCandidates = @(
    "Program.cs"
    "Startup.cs"
    "web.config"
    "Web.config"
)

Write-Host "[3/6] Comprovant directoris i fitxers importants de la API..."

$existingDirs = @()
foreach ($d in $dirCandidates) {
    $full = Join-Path $ProjectRoot $d
    if (Test-Path $full -PathType Container) {
        $existingDirs += $d
    }
}

$existingRootFiles = @()
foreach ($f in $rootFileCandidates) {
    $full = Join-Path $ProjectRoot $f
    if (Test-Path $full -PathType Leaf) {
        $existingRootFiles += $f
    }
}

$appsettings = Get-ChildItem -Path $ProjectRoot -Filter "appsettings*.json" -File -ErrorAction SilentlyContinue
$csproj      = Get-ChildItem -Path $ProjectRoot -Filter "*.csproj"        -File -ErrorAction SilentlyContinue
$sln         = Get-ChildItem -Path $ProjectRoot -Filter "*.sln"           -File -ErrorAction SilentlyContinue

$existingRootFiles += $appsettings.Name
$existingRootFiles += $csproj.Name
$existingRootFiles += $sln.Name

$existingRootFiles = $existingRootFiles | Sort-Object -Unique

###############################################################################
# 4) CRITERI D'ARXIUS "DE CODI"
###############################################################################
function Is-TextCodeFile {
    param(
        [string]$RelativePath
    )

    switch -Wildcard ($RelativePath) {
        "*.cs"      { return $true }
        "*.cshtml"  { return $true }
        "*.sql"     { return $true }
        "*.json"    { return $true }
        "*.config"  { return $true }
        "*.xml"     { return $true }
        "*.ini"     { return $true }
        "*.yml"     { return $true }
        "*.yaml"    { return $true }
        "*.txt"     { return $true }
        "*.md"      { return $true }
        "*.rst"     { return $true }
        "*.sh"      { return $true }
        "*.ps1"     { return $true }
        "*.cmd"     { return $true }
        "*.bat"     { return $true }
        default     { return $false }
    }
}

function Add-FileToSnapshot {
    param(
        [string]$FullPath,
        [string]$RelativePath,
        [string]$OutFile
    )

    Add-Content -Path $OutFile -Value ""
    Add-Content -Path $OutFile -Value "================================================================================"
    Add-Content -Path $OutFile -Value "FILE: $RelativePath"
    Add-Content -Path $OutFile -Value "--------------------------------------------------------------------------------"
    Get-Content -Path $FullPath -Raw | Add-Content -Path $OutFile
    Add-Content -Path $OutFile -Value ""
}

###############################################################################
# 5) ABOCAR CONTINGUT AL TXT
###############################################################################
Write-Host "[4/6] Abocant fitxers d'arrel de la API al TXT..."

$filesCount = 0

foreach ($rel in $existingRootFiles) {
    $full = Join-Path $ProjectRoot $rel

    if (-not (Test-Path $full -PathType Leaf)) {
        continue
    }

    if (-not (Is-TextCodeFile -RelativePath $rel)) {
        continue
    }

    Add-FileToSnapshot -FullPath $full -RelativePath $rel -OutFile $outFile
    $filesCount++
}

Write-Host "[5/6] Recorrent directoris de codi i abocant contingut..."

foreach ($d in $existingDirs) {
    $base = Join-Path $ProjectRoot $d
    Write-Host "    Processant directori: $d"

    Get-ChildItem -Path $base -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
        $file = $_.FullName
        $rel  = $file.Substring($ProjectRoot.Path.Length).TrimStart('\','/')

        if (-not (Is-TextCodeFile -RelativePath $rel)) {
            return
        }

        Add-FileToSnapshot -FullPath $file -RelativePath $rel -OutFile $outFile
        $filesCount++
    }
}

###############################################################################
# 6) RESUM
###############################################################################
Write-Host "[6/6] Export API completat."
Write-Host "    Fitxer generat: $outFile"
Write-Host "    Fitxers de codi/text abocats: $filesCount"

if ($filesCount -eq 0) {
    Write-Warning "No s'ha detectat cap fitxer de codi. Revisa que:"
    Write-Warning "  - Estiguis a l'arrel correcta de la API."
    Write-Warning "  - No hi hagi problemes de permisos."
}


# Executar amb ./_dmj_export_api_code.ps1