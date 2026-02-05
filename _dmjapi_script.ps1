# =========================
# DOMENJÓ · DMJAPI Patch · Fase 3 (fallback temporal headers)
# PowerShell 5.1
# =========================

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

Write-Host "=== DMJAPI · PATCH Fase 3 · AdminApiKey/ProfileApiKey + fallback X-Api-Key ==="

# [0] Recordatori backup (tu ja fas git, però aquest script fa .bak igualment)
$TS = (Get-Date).ToString("yyyyMMdd_HHmmss")

# --- Helpers ---
function Assert-File([string]$Path) {
  if (-not (Test-Path -LiteralPath $Path)) {
    throw "ERROR: No trobo el fitxer: $Path (executa el script des de l'arrel del projecte)."
  }
}

function Backup-File([string]$Path) {
  $bk = "$Path.bak_$TS"
  Copy-Item -LiteralPath $Path -Destination $bk -Force
  Write-Host "Backup: $bk"
}

function Replace-Once([string]$Path, [string]$Pattern, [string]$Replacement, [string]$Label) {
  $s = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
  $rx = New-Object System.Text.RegularExpressions.Regex($Pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
  $m = $rx.Matches($s)
  if ($m.Count -ne 1) {
    throw "ERROR: [$Label] esperava 1 match a $Path, però n'he trobat $($m.Count). No aplico el patch."
  }
  $s2 = $rx.Replace($s, $Replacement, 1)
  Set-Content -LiteralPath $Path -Value $s2 -Encoding UTF8
  Write-Host "OK: $Label"
}

# --- Targets (segons el teu codi) ---
$files = @(
  "Controllers\QRController.Loxone.cs",
  "Controllers\QrMultiAdminController.cs",
  "Controllers\QrResolveController.cs",
  "Controllers\IntranetSetup\ClientsSelfController.cs",
  "Infrastructure\ApiKeys\ApiKeyProfileMiddleware.cs"
)

Write-Host ""
Write-Host "[1/4] Validant fitxers..."
foreach ($f in $files) { Assert-File $f }

Write-Host ""
Write-Host "[2/4] Backups..."
foreach ($f in $files) { Backup-File $f }

Write-Host ""
Write-Host "[3/4] Aplicant canvis..."

# 3.1 QRController.Loxone.cs · IsAdminAuthorized() -> header preferent AdminApiKey
Replace-Once `
  "Controllers\QRController.Loxone.cs" `
  'var\s+header\s*=\s*Request\.Headers\["X-Api-Key"\]\.FirstOrDefault\(\)\s*\r?\n\s*\?\?\s*Request\.Headers\["X-API-Key"\]\.FirstOrDefault\(\)\s*\r?\n\s*\?\?\s*Request\.Headers\["x-api-key"\]\.FirstOrDefault\(\)\s*;' `
  'var header = Request.Headers["AdminApiKey"].FirstOrDefault()
                    ?? Request.Headers["X-Api-Key"].FirstOrDefault()
                    ?? Request.Headers["X-API-Key"].FirstOrDefault()
                    ?? Request.Headers["x-api-key"].FirstOrDefault();' `
  "QRController.Loxone.cs · Admin header fallback"

# 3.2 QrMultiAdminController.cs · IsAdminAuthorized() -> TryGetValue AdminApiKey abans
Replace-Once `
  "Controllers\QrMultiAdminController.cs" `
  'if\s*\(Request\.Headers\.TryGetValue\("X-Api-Key",\s*out\s+var\s+got\)\)\s*\r?\n\s*provided\s*=\s*got\.ToString\(\)\.Trim\(\);\s*\r?\n\s*else\s+if\s*\(Request\.Headers\.TryGetValue\("X-API-Key",\s*out\s+var\s+got2\)\)\s*\r?\n\s*provided\s*=\s*got2\.ToString\(\)\.Trim\(\);\s*' `
  'if (Request.Headers.TryGetValue("AdminApiKey", out var got0))
                provided = got0.ToString().Trim();
            else if (Request.Headers.TryGetValue("X-Api-Key", out var got))
                provided = got.ToString().Trim();
            else if (Request.Headers.TryGetValue("X-API-Key", out var got2))
                provided = got2.ToString().Trim();
' `
  "QrMultiAdminController.cs · AdminApiKey preferent"

# 3.3 QrResolveController.cs · TryAuthorizeClientKey() -> TryGetValue AdminApiKey abans
Replace-Once `
  "Controllers\QrResolveController.cs" `
  'if\s*\(Request\.Headers\.TryGetValue\("X-Api-Key",\s*out\s+var\s+apiKeyVal\)\)\s*\r?\n\s*provided\s*=\s*apiKeyVal\.ToString\(\)\.Trim\(\);\s*\r?\n\s*else\s+if\s*\(Request\.Headers\.TryGetValue\("X-API-Key",\s*out\s+var\s+showroomVal\)\)\s*\r?\n\s*provided\s*=\s*showroomVal\.ToString\(\)\.Trim\(\);\s*' `
  'if (Request.Headers.TryGetValue("AdminApiKey", out var adminVal))
                provided = adminVal.ToString().Trim();
            else if (Request.Headers.TryGetValue("X-Api-Key", out var apiKeyVal))
                provided = apiKeyVal.ToString().Trim();
            else if (Request.Headers.TryGetValue("X-API-Key", out var showroomVal))
                provided = showroomVal.ToString().Trim();
' `
  "QrResolveController.cs · AdminApiKey preferent"

# 3.4 ClientsSelfController.cs · CheckApiKey() -> afegir AdminApiKey com a primer header
Replace-Once `
  "Controllers\IntranetSetup\ClientsSelfController.cs" `
  'if\s*\(Request\.Headers\.TryGetValue\("X-Api-Key",\s*out\s+var\s+h1\)\)\s*key\s*=\s*h1\.ToString\(\);\s*' `
  'if (Request.Headers.TryGetValue("AdminApiKey", out var ha)) key = ha.ToString();
            else if (Request.Headers.TryGetValue("X-Api-Key", out var h1)) key = h1.ToString();' `
  "ClientsSelfController.cs · AdminApiKey preferent"

# 3.5 ApiKeyProfileMiddleware.cs · X-Api-Key -> ProfileApiKey preferent + fallback X-Api-Key
Replace-Once `
  "Infrastructure\ApiKeys\ApiKeyProfileMiddleware.cs" `
  '\/\/\s*Header\s*required\s*\r?\n\s*if\s*\(!ctx\.Request\.Headers\.TryGetValue\("X-Api-Key",\s*out\s+var\s+hv\)\)\s*\r?\n\s*\{\s*\r?\n\s*ctx\.Response\.StatusCode\s*=\s*StatusCodes\.Status401Unauthorized;\s*\r?\n\s*await\s+ctx\.Response\.WriteAsJsonAsync\([^;]*"Falta header X-Api-Key\."[^;]*\);\s*\r?\n\s*return;\s*\r?\n\s*\}\s*\r?\n\s*\r?\n\s*string\s+incoming\s*=\s*\(hv\.ToString\(\)\s*\?\?\s*""\)\.Trim\(\);\s*' `
  '// Header required (ProfileApiKey preferent; fallback temporal X-Api-Key)
            string incoming = "";
            if (ctx.Request.Headers.TryGetValue("ProfileApiKey", out var hvNew))
                incoming = (hvNew.ToString() ?? "").Trim();
            else if (ctx.Request.Headers.TryGetValue("X-Api-Key", out var hvOld))
                incoming = (hvOld.ToString() ?? "").Trim();

            if (string.IsNullOrWhiteSpace(incoming))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new { ok = false, code = "MISSING_API_KEY", message = "Falta header ProfileApiKey (o X-Api-Key durant la transició)." });
                return;
            }
' `
  "ApiKeyProfileMiddleware.cs · ProfileApiKey preferent"

Write-Host ""
Write-Host "[4/4] Build..."
dotnet build

Write-Host ""
Write-Host "=== PATCH OK · Fase 3 completada ==="
Write-Host "Proves manuals recomanades:"
Write-Host "  - Admin endpoint amb X-Api-Key (vell) -> OK"
Write-Host "  - Admin endpoint amb AdminApiKey (nou) -> OK"
Write-Host "  - Profile endpoint amb X-Api-Key (vell) -> OK"
Write-Host "  - Profile endpoint amb ProfileApiKey (nou) -> OK"








# executar amb ./_dmjapi_script.ps1

