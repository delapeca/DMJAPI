# _dmjapi_patch_02c_fix_crq_list_get.ps1
# DOMENJÓ · DMJAPI Patch 02C (FIX) · SapChangeRequestsUdo: GET list + GET get · PS 5.1 · NO python
$ErrorActionPreference = "Stop"
function Die($m){ Write-Host "ERROR: $m" -ForegroundColor Red; exit 1 }

Write-Host "=== DOMENJÓ · DMJAPI Patch 02C (FIX) · CRQ list/get ==="
$TS = Get-Date -Format "yyyyMMdd_HHmmss"
$Root = (Get-Location).Path

# [1/7] Localitza fitxers
Write-Host "`n[1/7] Localitzant Controller/Service..."
$ControllerPath = Join-Path $Root "Controllers\SapChangeRequestsUdoController.cs"
$ServicePath    = Join-Path $Root "Services\SapChangeRequestsUdoService.cs"
if (-not (Test-Path $ControllerPath)) { Die "No trobo: $ControllerPath" }
if (-not (Test-Path $ServicePath))    { Die "No trobo: $ServicePath" }

# [2/7] Restaura des del backup (agafa el .bak més recent)
Write-Host "`n[2/7] Restaurant des del .bak més recent..."
$ctrlBak = Get-ChildItem -Path $ControllerPath*.bak_* -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$svcBak  = Get-ChildItem -Path $ServicePath*.bak_* -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $ctrlBak) { Die "No trobo cap backup per Controller (.bak_*)." }
if (-not $svcBak)  { Die "No trobo cap backup per Service (.bak_*)." }

Copy-Item -LiteralPath $ctrlBak.FullName -Destination $ControllerPath -Force
Copy-Item -LiteralPath $svcBak.FullName  -Destination $ServicePath -Force
Write-Host "OK restore Controller <- $($ctrlBak.Name)"
Write-Host "OK restore Service    <- $($svcBak.Name)"

# [3/7] Backup post-restore
Write-Host "`n[3/7] Backups post-restore..."
Copy-Item -LiteralPath $ControllerPath -Destination "$ControllerPath.bak_$TS" -Force
Copy-Item -LiteralPath $ServicePath    -Destination "$ServicePath.bak_$TS" -Force

# Helper: inserir text dins la classe (brace matching)
function Insert-InClass {
  param([string]$Text, [string]$ClassName, [string]$InsertBlock)

  $idxClass = $Text.IndexOf("class $ClassName")
  if ($idxClass -lt 0) { Die "No trobo class $ClassName" }

  $idxOpen = $Text.IndexOf("{", $idxClass)
  if ($idxOpen -lt 0) { Die "No trobo '{' d'obertura de $ClassName" }

  $depth = 0
  $i = $idxOpen
  for (; $i -lt $Text.Length; $i++) {
    $ch = $Text[$i]
    if ($ch -eq "{") { $depth++ }
    elseif ($ch -eq "}") {
      $depth--
      if ($depth -eq 0) {
        # $i és el '}' que tanca la classe
        return $Text.Insert($i, "`r`n$InsertBlock`r`n")
      }
    }
  }
  Die "No he pogut fer brace-match de la classe $ClassName"
}

# [4/7] Patch Controller
Write-Host "`n[4/7] Patch Controller..."
$ctrl = Get-Content -LiteralPath $ControllerPath -Raw -Encoding UTF8

# Treu using incorrecte si existís
$ctrl = $ctrl -replace "^\s*using\s+XNDmjApi\.Models\.ChangeRequests;\s*\r?\n", ""

# Si ja hi ha list/get, no dupliquem
if ($ctrl -match '\[HttpGet\("list"\)\]' -or $ctrl -match '\[HttpGet\("get"\)\]') {
  Write-Host "Controller ja conté GET list/get. Skip inserció."
} else {
  $insertCtrl = @'
        [HttpGet("list")]
        public IActionResult List(
            [FromQuery] string cardCode,
            [FromQuery] string? kind = null,
            [FromQuery] int? targetId = null,
            [FromQuery] string? status = null,
            [FromQuery] int skip = 0,
            [FromQuery] int top = 50,
            [FromQuery] string? order = "desc")
        {
            var profile = HttpContext.Items[ApiKeyProfileMiddleware.HttpContextItemKey] as ApiKeyProfile;
            if (profile == null)
                return Unauthorized(new { ok = false, code = "MISSING_PROFILE", message = "Falta perfil (X-Api-Key)." });

            bool orderDesc = !(order ?? "desc").Trim().Equals("asc", System.StringComparison.OrdinalIgnoreCase);

            var (ok, code, message, data) = _svc.List(profile, cardCode, kind, targetId, status, skip, top, orderDesc);

            if (!ok)
                return BadRequest(new ChangeRequestListResponse { Ok = false, Code = code, Message = message, CardCode = cardCode, Items = new System.Collections.Generic.List<ChangeRequestListItemDto>(), Paging = null });

            return Ok(data);
        }

        [HttpGet("get")]
        public IActionResult Get([FromQuery] string requestRef)
        {
            var profile = HttpContext.Items[ApiKeyProfileMiddleware.HttpContextItemKey] as ApiKeyProfile;
            if (profile == null)
                return Unauthorized(new { ok = false, code = "MISSING_PROFILE", message = "Falta perfil (X-Api-Key)." });

            var (ok, code, message, data) = _svc.Get(profile, requestRef);

            if (!ok && code == "NOT_FOUND")
                return NotFound(new ChangeRequestGetResponse { Ok = false, Code = code, Message = message, Item = null });

            if (!ok)
                return BadRequest(new ChangeRequestGetResponse { Ok = false, Code = code, Message = message, Item = null });

            return Ok(data);
        }
'@
  $ctrl = Insert-InClass -Text $ctrl -ClassName "SapChangeRequestsUdoController" -InsertBlock $insertCtrl
  Write-Host "OK: afegits mètodes GET list/get al controller."
}

Set-Content -LiteralPath $ControllerPath -Value $ctrl -Encoding UTF8

# [5/7] Patch Service (List/Get amb out int)
Write-Host "`n[5/7] Patch Service..."
$svc = Get-Content -LiteralPath $ServicePath -Raw -Encoding UTF8

# Treu using incorrecte si existís
$svc = $svc -replace "^\s*using\s+XNDmjApi\.Models\.ChangeRequests;\s*\r?\n", ""

if ($svc -match "ChangeRequestListResponse\?\s+data\)\s+List\(" -and $svc -match "ChangeRequestGetResponse\?\s+data\)\s+Get\(") {
  Write-Host "Service ja conté List/Get. Skip inserció."
} else {
  $anchor = [regex]::Match($svc, "\r?\n\s*private\s+static\s+\(bool\s+ok,\s*string\?\s*err\)\s+ConnectCompany\(", "Singleline")
  if (-not $anchor.Success) { Die "No trobo ConnectCompany(...) per inserir List/Get." }

  $insertSvc = @'
        public (bool ok, string code, string message, ChangeRequestListResponse? data) List(
            ApiKeyProfile profile,
            string cardCode,
            string? kind,
            int? targetId,
            string? status,
            int skip,
            int top,
            bool orderDesc)
        {
            if (profile == null) return (false, "MISSING_PROFILE", "Falta perfil (X-Api-Key).", null);
            if (string.IsNullOrWhiteSpace(cardCode)) return (false, "MISSING_CARDCODE", "Falta cardCode.", null);

            if (skip < 0) skip = 0;
            if (top <= 0) top = 50;
            if (top > 200) top = 200;

            Company? company = null;
            Recordset? rs = null;

            try
            {
                var (okConn, err) = ConnectCompany(profile, out company);
                if (!okConn || company == null)
                    return (false, err ?? "SAP_CONNECT_FAILED", "No es pot connectar a SAP (DI-API) amb el perfil.", null);

                string cc = cardCode.Trim().Replace("'", "''");
                string? kind2 = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim();
                string? status2 = string.IsNullOrWhiteSpace(status) ? null : status.Trim();

                string where = $"U_CardCode = '{cc}'";

                if (!string.IsNullOrWhiteSpace(kind2))
                    where += $" AND U_Kind = '{kind2.Replace("'", "''")}'";

                if (targetId.HasValue)
                    where += $" AND U_TargetId = {targetId.Value}";

                if (!string.IsNullOrWhiteSpace(status2))
                    where += $" AND U_Status = '{status2.Replace("'", "''")}'";

                int from = skip + 1;
                int to = skip + top;

                string order = orderDesc ? "DESC" : "ASC";

                string sql = $@"
SELECT
  U_RequestRef,
  U_CardCode,
  U_Kind,
  U_Action,
  U_TargetId,
  U_TargetName,
  U_Status,
  U_RequestedAtUtc,
  U_DecisionAtUtc,
  U_DecisionNote,
  U_ApplyAtUtc,
  U_ApplyError
FROM
(
    SELECT
      ROW_NUMBER() OVER (ORDER BY T.CreateDate {order}, T.CreateTime {order}, T.DocEntry {order}) AS RN,
      T.U_RequestRef,
      T.U_CardCode,
      T.U_Kind,
      T.U_Action,
      T.U_TargetId,
      T.U_TargetName,
      T.U_Status,
      T.U_RequestedAtUtc,
      T.U_DecisionAtUtc,
      T.U_DecisionNote,
      T.U_ApplyAtUtc,
      T.U_ApplyError
    FROM [@XN_CRQ] T
    WHERE {where}
) X
WHERE X.RN BETWEEN {from} AND {to}
ORDER BY X.RN";

                rs = (Recordset)company.GetBusinessObject(BoObjectTypes.BoRecordset);
                rs.DoQuery(sql);

                var resp = new ChangeRequestListResponse
                {
                    Ok = true,
                    Code = "OK",
                    Message = "OK",
                    CardCode = cardCode.Trim()
                };

                int count = 0;
                while (!rs.EoF)
                {
                    var item = new ChangeRequestListItemDto
                    {
                        RequestRef = Convert.ToString(rs.Fields.Item("U_RequestRef").Value)?.Trim(),
                        CardCode = Convert.ToString(rs.Fields.Item("U_CardCode").Value)?.Trim(),
                        Kind = Convert.ToString(rs.Fields.Item("U_Kind").Value)?.Trim(),
                        Action = Convert.ToString(rs.Fields.Item("U_Action").Value)?.Trim(),
                        Status = Convert.ToString(rs.Fields.Item("U_Status").Value)?.Trim(),
                        TargetName = Convert.ToString(rs.Fields.Item("U_TargetName").Value)?.Trim(),
                        RequestedAtUtc = Convert.ToString(rs.Fields.Item("U_RequestedAtUtc").Value)?.Trim(),
                        DecisionAtUtc = Convert.ToString(rs.Fields.Item("U_DecisionAtUtc").Value)?.Trim(),
                        DecisionNote = Convert.ToString(rs.Fields.Item("U_DecisionNote").Value)?.Trim(),
                        ApplyAtUtc = Convert.ToString(rs.Fields.Item("U_ApplyAtUtc").Value)?.Trim(),
                        ApplyError = Convert.ToString(rs.Fields.Item("U_ApplyError").Value)?.Trim(),
                    };

                    try
                    {
                        var v = rs.Fields.Item("U_TargetId").Value;
                        int tid;
                        if (v != null && int.TryParse(Convert.ToString(v), out tid)) item.TargetId = tid;
                    }
                    catch { }

                    resp.Items.Add(item);
                    count++;
                    rs.MoveNext();
                }

                resp.Paging = new ChangeRequestPagingDto
                {
                    Skip = skip,
                    Top = top,
                    Count = count,
                    HasMore = (count == top)
                };

                return (true, "OK", "OK", resp);
            }
            catch (Exception ex)
            {
                return (false, "EXCEPTION", ex.Message, null);
            }
            finally
            {
                if (rs != null) Marshal.ReleaseComObject(rs);

                try
                {
                    if (company != null)
                    {
                        if (company.Connected) company.Disconnect();
                        Marshal.ReleaseComObject(company);
                    }
                }
                catch { }
            }
        }

        public (bool ok, string code, string message, ChangeRequestGetResponse? data) Get(ApiKeyProfile profile, string requestRef)
        {
            if (profile == null) return (false, "MISSING_PROFILE", "Falta perfil (X-Api-Key).", null);
            if (string.IsNullOrWhiteSpace(requestRef)) return (false, "MISSING_REQUESTREF", "Falta requestRef.", null);

            Company? company = null;
            Recordset? rsH = null;
            Recordset? rsL = null;

            try
            {
                var (okConn, err) = ConnectCompany(profile, out company);
                if (!okConn || company == null)
                    return (false, err ?? "SAP_CONNECT_FAILED", "No es pot connectar a SAP (DI-API) amb el perfil.", null);

                string rr = requestRef.Trim().Replace("'", "''");

                string sqlH = $@"
SELECT TOP 1
  Code,
  U_RequestRef,
  U_CardCode,
  U_Kind,
  U_Action,
  U_TargetId,
  U_TargetName,
  U_TargetJson,
  U_Reason,
  U_RequestedByUserId,
  U_RequestedByEmail,
  U_Status,
  U_RequestedAtUtc,
  U_DecisionAtUtc,
  U_DecisionNote,
  U_ApplyAtUtc,
  U_ApplyError
FROM [@XN_CRQ]
WHERE Code = '{rr}' OR U_RequestRef = '{rr}'
ORDER BY DocEntry DESC";

                rsH = (Recordset)company.GetBusinessObject(BoObjectTypes.BoRecordset);
                rsH.DoQuery(sqlH);

                if (rsH.RecordCount <= 0)
                    return (false, "NOT_FOUND", "No existeix requestRef.", null);

                string code = Convert.ToString(rsH.Fields.Item("Code").Value)?.Trim() ?? rr;
                string codeSql = code.Replace("'", "''");

                var item = new ChangeRequestDetailDto
                {
                    RequestRef = Convert.ToString(rsH.Fields.Item("U_RequestRef").Value)?.Trim(),
                    CardCode = Convert.ToString(rsH.Fields.Item("U_CardCode").Value)?.Trim(),
                    Kind = Convert.ToString(rsH.Fields.Item("U_Kind").Value)?.Trim(),
                    Action = Convert.ToString(rsH.Fields.Item("U_Action").Value)?.Trim(),
                    TargetName = Convert.ToString(rsH.Fields.Item("U_TargetName").Value)?.Trim(),
                    TargetJson = Convert.ToString(rsH.Fields.Item("U_TargetJson").Value),
                    Reason = Convert.ToString(rsH.Fields.Item("U_Reason").Value)?.Trim(),
                    RequestedByEmail = Convert.ToString(rsH.Fields.Item("U_RequestedByEmail").Value)?.Trim(),
                    Status = Convert.ToString(rsH.Fields.Item("U_Status").Value)?.Trim(),
                    RequestedAtUtc = Convert.ToString(rsH.Fields.Item("U_RequestedAtUtc").Value)?.Trim(),
                    DecisionAtUtc = Convert.ToString(rsH.Fields.Item("U_DecisionAtUtc").Value)?.Trim(),
                    DecisionNote = Convert.ToString(rsH.Fields.Item("U_DecisionNote").Value)?.Trim(),
                    ApplyAtUtc = Convert.ToString(rsH.Fields.Item("U_ApplyAtUtc").Value)?.Trim(),
                    ApplyError = Convert.ToString(rsH.Fields.Item("U_ApplyError").Value)?.Trim(),
                };

                try
                {
                    var v = rsH.Fields.Item("U_TargetId").Value;
                    int tid;
                    if (v != null && int.TryParse(Convert.ToString(v), out tid)) item.TargetId = tid;
                }
                catch { }

                try
                {
                    var v = rsH.Fields.Item("U_RequestedByUserId").Value;
                    int uid;
                    if (v != null && int.TryParse(Convert.ToString(v), out uid)) item.RequestedByUserId = uid;
                }
                catch { }

                string sqlL = $@"
SELECT
  U_Entity,
  U_Field,
  U_OldValue,
  U_NewValue,
  U_IsSensitive,
  U_LineStatus,
  U_LineError
FROM [@XN_CRQ1]
WHERE Code = '{codeSql}'
ORDER BY LineId ASC";

                rsL = (Recordset)company.GetBusinessObject(BoObjectTypes.BoRecordset);
                rsL.DoQuery(sqlL);

                while (!rsL.EoF)
                {
                    string isSens = Convert.ToString(rsL.Fields.Item("U_IsSensitive").Value)?.Trim() ?? "";
                    bool bSens = isSens == "1" || isSens.Equals("Y", StringComparison.OrdinalIgnoreCase);

                    item.Lines.Add(new ChangeRequestLineItemDto
                    {
                        Entity = Convert.ToString(rsL.Fields.Item("U_Entity").Value)?.Trim(),
                        Field = Convert.ToString(rsL.Fields.Item("U_Field").Value)?.Trim(),
                        OldValue = Convert.ToString(rsL.Fields.Item("U_OldValue").Value),
                        NewValue = Convert.ToString(rsL.Fields.Item("U_NewValue").Value),
                        IsSensitive = bSens,
                        LineStatus = Convert.ToString(rsL.Fields.Item("U_LineStatus").Value)?.Trim(),
                        LineError = Convert.ToString(rsL.Fields.Item("U_LineError").Value),
                    });

                    rsL.MoveNext();
                }

                var resp = new ChangeRequestGetResponse
                {
                    Ok = true,
                    Code = "OK",
                    Message = "OK",
                    Item = item
                };

                return (true, "OK", "OK", resp);
            }
            catch (Exception ex)
            {
                return (false, "EXCEPTION", ex.Message, null);
            }
            finally
            {
                if (rsL != null) Marshal.ReleaseComObject(rsL);
                if (rsH != null) Marshal.ReleaseComObject(rsH);

                try
                {
                    if (company != null)
                    {
                        if (company.Connected) company.Disconnect();
                        Marshal.ReleaseComObject(company);
                    }
                }
                catch { }
            }
        }

'@

  $svc = $svc.Insert($anchor.Index, "`r`n$insertSvc`r`n")
  Set-Content -LiteralPath $ServicePath -Value $svc -Encoding UTF8
  Write-Host "OK: afegits List/Get al service."
}

# [6/7] Verificació d'aplicació
Write-Host "`n[6/7] Verificant que s'ha aplicat..."
$ctrlNow = Get-Content -LiteralPath $ControllerPath -Raw -Encoding UTF8
if ($ctrlNow -notmatch '\[HttpGet\("list"\)\]' -or $ctrlNow -notmatch '\[HttpGet\("get"\)\]') { Die "Controller sense list/get." }
Write-Host "OK: Controller té list/get."

$svcNow = Get-Content -LiteralPath $ServicePath -Raw -Encoding UTF8
if ($svcNow -notmatch "ChangeRequestListResponse\?\s+data\)\s+List\(" -or $svcNow -notmatch "ChangeRequestGetResponse\?\s+data\)\s+Get\(") { Die "Service sense List/Get." }
Write-Host "OK: Service té List/Get."

# [7/7] Build
Write-Host "`n[7/7] dotnet build..."
$sln = Get-ChildItem -Path $Root -Filter *.sln -File -ErrorAction SilentlyContinue | Select-Object -First 1
$csproj = Get-ChildItem -Path $Root -Recurse -Filter *.csproj -File -ErrorAction SilentlyContinue | Select-Object -First 1
if ($sln) { dotnet build $sln.FullName } else { dotnet build $csproj.FullName }

Write-Host "`nOK · Patch 02C aplicat."
Write-Host "Mira Swagger: SapChangeRequestsUdo -> GET /api/SapChangeRequestsUdo/list i GET /api/SapChangeRequestsUdo/get"







# executar amb ./_dmjapi_script.ps1

