#requires -Version 5.1
$ErrorActionPreference = "Stop"

$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$outFile = Join-Path (Get-Location) "_dmj_patch_out.txt"

function Log($s) {
  $line = "[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $s
  Write-Host $line
  Add-Content -Path $outFile -Encoding UTF8 -Value $line
}

Log "DMJ PATCH: CRQ Apply endpoint (controller) v1"
Log "RepoRoot: $(Get-Location)"

# 1) Target file
$CTRL = "Controllers\SapChangeRequestsUdoController.cs"
if (!(Test-Path $CTRL)) { throw "ERROR: No existeix el fitxer: $CTRL" }

# 2) Backup
$bak = "$CTRL.bak_$ts"
Copy-Item -LiteralPath $CTRL -Destination $bak -Force
Log "Backup: $bak"

# 3) Load file
$code = Get-Content -LiteralPath $CTRL -Raw -Encoding UTF8

# 4) Guard rails
if ($code -match '\[HttpPost\("apply"\)\]') {
  Log "Ja existeix [HttpPost(""apply"")]. No faig res."
  exit 0
}

# 5) Insert Apply request DTO (next to ChangeRequestSetLineStatusRequest)
$dtoNeedle = "public sealed class ChangeRequestSetLineStatusRequest"
if ($code -notmatch [regex]::Escape($dtoNeedle)) { throw "ERROR: No trobo ChangeRequestSetLineStatusRequest al controller." }

$applyDto = @"
public sealed class ChangeRequestApplyRequest
{
    public string? RequestRef { get; set; }
    public string? CardCode { get; set; }
}

"@

# Inserim just abans de ChangeRequestSetLineStatusRequest
$code2 = [regex]::Replace(
  $code,
  "(\r?\n)(\s*public\s+sealed\s+class\s+ChangeRequestSetLineStatusRequest\s*\r?\n)",
  "`$1$applyDto`$2",
  1
)

if ($code2 -eq $code) { throw "ERROR: No he pogut inserir ChangeRequestApplyRequest." }
$code = $code2

# 6) Insert Apply endpoint method inside controller class, after SetLineStatus method
# Troba el final del mètode SetLineStatus (return Ok...) i el tanca abans del final de classe.
$applyMethod = @"

        [HttpPost(""apply"")]
        public IActionResult Apply([FromBody] ChangeRequestApplyRequest req)
        {
            var profile = HttpContext.Items[ApiKeyProfileMiddleware.HttpContextItemKey] as ApiKeyProfile;
            if (profile == null)
                return Unauthorized(new { ok = false, code = ""MISSING_PROFILE"", message = ""Falta perfil (ProfileApiKey / X-Api-Key)."" });

            if (req == null)
                return BadRequest(new { ok = false, code = ""MISSING_BODY"", message = ""Falta body."" });

            var requestRef = (req.RequestRef ?? """").Trim();
            var cardCode = (req.CardCode ?? """").Trim();

            if (string.IsNullOrWhiteSpace(requestRef))
                return BadRequest(new { ok = false, code = ""MISSING_REQUESTREF"", message = ""Falta requestRef."" });

            // cardCode és opcional, però si el passes, fem match al service
            var (ok, c, m) = _svc.Apply(profile, requestRef, string.IsNullOrWhiteSpace(cardCode) ? null : cardCode);

            if (!ok)
                return BadRequest(new { ok = false, code = c, message = m });

            return Ok(new { ok = true, code = c, message = m });
        }

"@

# Inserim abans de l'última '}' que tanca la classe controller (la de SapChangeRequestsUdoController),
# mantenint la '}' final del namespace.
$code3 = [regex]::Replace(
  $code,
  "(\r?\n\s*)}\s*\r?\n}\s*\r?\n\s*$",
  "`$1$applyMethod`$1}`r`n}`r`n",
  1
)

if ($code3 -eq $code) { throw "ERROR: No he pogut inserir el mètode Apply dins la classe." }
$code = $code3

# 7) Write back
Set-Content -LiteralPath $CTRL -Value $code -Encoding UTF8
Log "Patched: $CTRL"

# 8) dotnet build (minimal test)
Log "Running: dotnet build"
$build = & dotnet build 2>&1
$build | ForEach-Object { Add-Content -Path $outFile -Encoding UTF8 -Value $_ }
if ($LASTEXITCODE -ne 0) {
  Log "ERROR: dotnet build ha fallat. Reverteix amb el .bak i passa'm el _dmj_patch_out.txt"
  exit 1
}
Log "OK: dotnet build"

Log "DONE"





# executar amb ./_dmjapi_script.ps1

