# _dmj_patch_crq_set_line_status_v1.ps1
# PowerShell 5.1 compatible

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- paths (as provided) ---
$SVC  = "E:\Programacio\C#\XNApiRest\Services\SapChangeRequestsUdoService.cs"
$CTRL = "E:\Programacio\C#\XNApiRest\Controllers\SapChangeRequestsUdoController.cs"

# --- encoding helpers (UTF-8 no BOM) ---
function Read-FileUtf8NoBom([string]$path) {
  return [System.IO.File]::ReadAllText($path, [System.Text.UTF8Encoding]::new($false))
}
function Write-FileUtf8NoBom([string]$path, [string]$content) {
  [System.IO.File]::WriteAllText($path, $content, [System.Text.UTF8Encoding]::new($false))
}

# --- preflight ---
foreach ($p in @($SVC,$CTRL)) {
  if (!(Test-Path -LiteralPath $p)) { throw "ERROR: file not found: $p" }
}

$ts = (Get-Date).ToString("yyyyMMdd_HHmmss")
Write-Host "1) Backup (.bak_$ts)"
Copy-Item -LiteralPath $SVC  -Destination "$SVC.bak_$ts"  -Force
Copy-Item -LiteralPath $CTRL -Destination "$CTRL.bak_$ts" -Force

Write-Host "2) Patch SapChangeRequestsUdoService.cs + Controller.cs"

# -------------------------
# Patch SERVICE
# -------------------------
$svcText = Read-FileUtf8NoBom $SVC

if ($svcText -match 'SetLineStatus\s*\(' -or $svcText -match 'set-line-status') {
  throw "ERROR: Patch seems already applied (SetLineStatus / set-line-status found)."
}

$insertBefore = 'private\s+static\s+\(bool\s+ok,\s*string\?\s*err\)\s+ConnectCompany\s*\('
$m = [regex]::Match($svcText, $insertBefore)
if (!$m.Success) { throw "ERROR: Could not find insertion point in service (ConnectCompany)." }

$serviceBlock = @"
        public (bool ok, string code, string message) SetLineStatus(
            ApiKeyProfile profile,
            string cardCode,
            string code,
            int lineId,
            string lineStatus)
        {
            if (profile == null) return (false, "MISSING_PROFILE", "Falta perfil (ProfileApiKey / X-Api-Key).");
            if (string.IsNullOrWhiteSpace(cardCode)) return (false, "MISSING_CARDCODE", "Falta cardCode.");
            if (string.IsNullOrWhiteSpace(code)) return (false, "MISSING_CODE", "Falta code.");
            if (lineId < 0) return (false, "INVALID_LINEID", "lineId invàlid.");

            string st = (lineStatus ?? "").Trim().ToUpperInvariant();
            if (st != "VALIDATED" && st != "REJECTED")
                return (false, "INVALID_STATUS", "lineStatus ha de ser VALIDATED o REJECTED.");

            Company? company = null;
            CompanyService? companyService = null;
            GeneralService? generalService = null;
            GeneralDataParams? key = null;
            GeneralData? data = null;
            GeneralDataCollection? lines = null;

            try
            {
                var (okConn, err) = ConnectCompany(profile, out company);
                if (!okConn || company == null)
                    return (false, err ?? "SAP_CONNECT_FAILED", "No es pot connectar a SAP (DI-API) amb el perfil.");

                companyService = company.GetCompanyService();
                generalService = companyService.GetGeneralService(UdoCode);

                key = (GeneralDataParams)generalService.GetDataInterface(GeneralServiceDataInterfaces.gsGeneralDataParams);
                key.SetProperty("Code", code.Trim());

                data = generalService.GetByParams(key);

                // Validate CardCode matches the request owner
                string cc = "";
                try { cc = (data.GetProperty("U_CardCode")?.ToString() ?? "").Trim(); } catch { }
                if (!cc.Equals(cardCode.Trim(), StringComparison.OrdinalIgnoreCase))
                    return (false, "NOT_FOUND", "No existeix CRQ amb aquest cardCode + code.");

                lines = data.Child(LTable);

                bool found = false;

                // Find the line by SAP child table LineId (system field)
                for (int i = 0; i < lines.Count; i++)
                {
                    var ln = lines.Item(i);

                    int currentLineId = -1;

                    try
                    {
                        var v = ln.GetProperty("LineId");
                        if (v != null && int.TryParse(Convert.ToString(v), out int lid))
                            currentLineId = lid;
                    }
                    catch { /* ignore */ }

                    if (currentLineId == lineId)
                    {
                        ln.SetProperty("U_LineStatus", st);
                        found = true;
                        break;
                    }
                }

                if (!found)
                    return (false, "LINE_NOT_FOUND", "No existeix la línia amb aquest lineId.");

                generalService.Update(data);
                return (true, "OK", "LineStatus actualitzat.");
            }
            catch (Exception ex)
            {
                return (false, "EXCEPTION", ex.Message);
            }
            finally
            {
                if (lines != null) Marshal.ReleaseComObject(lines);
                if (data != null) Marshal.ReleaseComObject(data);
                if (key != null) Marshal.ReleaseComObject(key);
                if (generalService != null) Marshal.ReleaseComObject(generalService);
                if (companyService != null) Marshal.ReleaseComObject(companyService);

                try
                {
                    if (company != null)
                    {
                        if (company.Connected) company.Disconnect();
                        Marshal.ReleaseComObject(company);
                    }
                }
                catch { /* ignore */ }
            }
        }

"@

$svcPatched = $svcText.Insert($m.Index, $serviceBlock)
Write-FileUtf8NoBom $SVC $svcPatched

# -------------------------
# Patch CONTROLLER
# -------------------------
$ctrlText = Read-FileUtf8NoBom $CTRL

if ($ctrlText -match 'set-line-status' -or $ctrlText -match 'SetLineStatus\s*\(') {
  throw "ERROR: Patch seems already applied in controller (set-line-status / SetLineStatus found)."
}

# Insert request DTO near top (inside namespace, before controller class)
$ctrlClassDecl = 'public\s+sealed\s+class\s+SapChangeRequestsUdoController\s*:\s*ControllerBase'
$mc = [regex]::Match($ctrlText, $ctrlClassDecl)
if (!$mc.Success) { throw "ERROR: Could not find controller class declaration." }

$dtoBlock = @"
    public sealed class ChangeRequestSetLineStatusRequest
    {
        public string? CardCode { get; set; }
        public string? Code { get; set; }
        public int LineId { get; set; }
        public string? LineStatus { get; set; }
    }

"@

# Put DTO block just above controller class declaration line
$ctrlText2 = $ctrlText.Insert($mc.Index, $dtoBlock)

# Insert endpoint before final closing braces of controller class (before the last "\n}")
# We’ll insert right before the last occurrence of "}\n}" that closes class+namespace.
$tailMatch = [regex]::Match($ctrlText2, "\r?\n\}\r?\n\}\s*$")
if (!$tailMatch.Success) { throw "ERROR: Could not find controller tail (class+namespace closing braces)." }

$endpointBlock = @"

        [HttpPost("set-line-status")]
        public IActionResult SetLineStatus([FromBody] ChangeRequestSetLineStatusRequest req)
        {
            var profile = HttpContext.Items[ApiKeyProfileMiddleware.HttpContextItemKey] as ApiKeyProfile;
            if (profile == null)
                return Unauthorized(new { ok = false, code = "MISSING_PROFILE", message = "Falta perfil (ProfileApiKey / X-Api-Key)." });

            if (req == null)
                return BadRequest(new { ok = false, code = "MISSING_BODY", message = "Falta body." });

            var cardCode = (req.CardCode ?? "").Trim();
            var code = (req.Code ?? "").Trim();
            var lineStatus = (req.LineStatus ?? "").Trim();

            if (string.IsNullOrWhiteSpace(cardCode))
                return BadRequest(new { ok = false, code = "MISSING_CARDCODE", message = "Falta cardCode." });

            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { ok = false, code = "MISSING_CODE", message = "Falta code." });

            var (ok, c, m) = _svc.SetLineStatus(profile, cardCode, code, req.LineId, lineStatus);

            if (!ok && (c == "NOT_FOUND" || c == "LINE_NOT_FOUND"))
                return NotFound(new { ok = false, code = c, message = m });

            if (!ok && (c == "INVALID_STATUS" || c == "INVALID_LINEID" || c == "MISSING_CARDCODE" || c == "MISSING_CODE"))
                return BadRequest(new { ok = false, code = c, message = m });

            if (!ok)
                return BadRequest(new { ok = false, code = c, message = m });

            return Ok(new { ok = true, code = c, message = m });
        }

"@

$insertPos = $tailMatch.Index
$ctrlPatched = $ctrlText2.Insert($insertPos, $endpointBlock)
Write-FileUtf8NoBom $CTRL $ctrlPatched

Write-Host "3) dotnet build (repo root inferred)"
$repoRoot = Split-Path -Parent (Split-Path -Parent $SVC)  # ...\XNApiRest
Push-Location $repoRoot
try {
  dotnet build
} finally {
  Pop-Location
}

Write-Host "DONE: set-line-status endpoint added."
Write-Host "Try:"
Write-Host '  POST /api/SapChangeRequestsUdo/set-line-status'
Write-Host '  Header: ProfileApiKey: ...'
Write-Host '  Body: {"cardCode":"C0001","code":"CRQ-20260213-1200-ABC123","lineId":0,"lineStatus":"VALIDATED"}'







# executar amb ./_dmjapi_script.ps1

