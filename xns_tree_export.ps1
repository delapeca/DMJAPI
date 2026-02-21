# tree-extract.ps1
# Genera l'arbre complet d'un projecte (exclou node_modules, .git, etc.)

param(
    [string]$Path = ".",
    [string]$Output = "tree_output.txt"
)

$excludeDirs = @('node_modules', '.git', '.next', 'dist', 'build', '.nuxt', '__pycache__', '.venv', 'vendor', 'coverage', '.turbo')

function Get-Tree {
    param(
        [string]$Dir,
        [string]$Prefix = ""
    )

    $items = Get-ChildItem -Path $Dir -Force | Where-Object {
        -not ($_.PSIsContainer -and $excludeDirs -contains $_.Name) -and
        -not ($_.Name -match '^\.')
    } | Sort-Object { $_.PSIsContainer } -Descending

    for ($i = 0; $i -lt $items.Count; $i++) {
        $item = $items[$i]
        $isLast = ($i -eq $items.Count - 1)
        $connector = if ($isLast) { "└── " } else { "├── " }
        $newPrefix = if ($isLast) { "$Prefix    " } else { "$Prefix│   " }

        Write-Output "$Prefix$connector$($item.Name)"

        if ($item.PSIsContainer) {
            Get-Tree -Dir $item.FullName -Prefix $newPrefix
        }
    }
}

$projectName = (Get-Item $Path).Name
$result = @("$projectName/")
$result += Get-Tree -Dir (Resolve-Path $Path)

$result | Out-File -FilePath $Output -Encoding UTF8
Write-Host "✅ Arbre generat a: $Output ($($result.Count) línies)" -ForegroundColor Green

# Exemple d'ús: ./xns_tree_export.ps1
