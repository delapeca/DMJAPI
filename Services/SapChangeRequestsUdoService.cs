using Newtonsoft.Json;
using SAPbobsCOM;
using System;
using System.Runtime.InteropServices;
using XNDmjApi.Infrastructure.ApiKeys;
using XNDmjApi.Models;
using XNDmjApi.Models.ChangeRequestsUdo;

namespace XNDmjApi.Services
{
    /// <summary>
    /// Extranet: Create + UpdateLines (només si Status=PENDING)
    /// Connexió DI-API amb usuari tècnic resolt per X-Api-Key (perfil), sense Dades.oCompany.
    /// </summary>
    public sealed class SapChangeRequestsUdoService
    {
        // UDO/UDT names (sense @) — segons EnsureSchema (XN_CRQ + XN_CRQ1)
        private const string UdoCode = "XN_CRQ";
        private const string LTable = "XN_CRQ1";

        public (bool ok, string code, string message, string? requestRef) Create(ApiKeyProfile profile, ChangeRequestUdoUpsertRequest req)
        {
            if (profile == null) return (false, "MISSING_PROFILE", "Falta perfil (X-Api-Key).", null);
            if (req == null) return (false, "MISSING_BODY", "Falta body.", null);

            if (string.IsNullOrWhiteSpace(req.CardCode)) return (false, "MISSING_CARDCODE", "Falta cardCode.", null);
            if (string.IsNullOrWhiteSpace(req.Kind)) return (false, "MISSING_KIND", "Falta kind.", null);
            if (string.IsNullOrWhiteSpace(req.Action)) return (false, "MISSING_ACTION", "Falta action (create/update).", null);

            string action = req.Action.Trim().ToLowerInvariant();
            if (action != "create" && action != "update") return (false, "INVALID_ACTION", "action ha de ser create/update.", null);

            string requestRef = BuildRequestRef();

            Company? company = null;
            CompanyService? companyService = null;
            GeneralService? generalService = null;
            GeneralData? data = null;
            GeneralDataCollection? lines = null;

            try
            {
                var (okConn, err) = ConnectCompany(profile, out company);
                if (!okConn || company == null)
                    return (false, err ?? "SAP_CONNECT_FAILED", "No es pot connectar a SAP (DI-API) amb el perfil.", null);

                companyService = company.GetCompanyService();
                generalService = companyService.GetGeneralService(UdoCode);

                data = (GeneralData)generalService.GetDataInterface(GeneralServiceDataInterfaces.gsGeneralData);

                // MasterData UDO: clau = Code
                data.SetProperty("Code", requestRef);
                data.SetProperty("Name", requestRef);

                // Header U_*
                data.SetProperty("U_RequestRef", requestRef);
                data.SetProperty("U_CardCode", req.CardCode.Trim());
                data.SetProperty("U_Kind", req.Kind.Trim());
                data.SetProperty("U_Action", action);
                data.SetProperty("U_RequestedAtUtc", global::System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                if (req.TargetId.HasValue) data.SetProperty("U_TargetId", req.TargetId.Value);
                if (!string.IsNullOrWhiteSpace(req.TargetName)) data.SetProperty("U_TargetName", req.TargetName.Trim());
                if (req.TargetJson != null)
                {
                    string targetJsonStr;

                    if (req.TargetJson is System.Text.Json.JsonElement je)
                        targetJsonStr = je.GetRawText();
                    else if (req.TargetJson is System.Text.Json.JsonDocument jd)
                        targetJsonStr = jd.RootElement.GetRawText();
                    else
                        targetJsonStr = JsonConvert.SerializeObject(req.TargetJson);

                    data.SetProperty("U_TargetJson", targetJsonStr);
                }

                if (!string.IsNullOrWhiteSpace(req.Reason)) data.SetProperty("U_Reason", req.Reason.Trim());

                if (req.RequestedBy != null)
                {
                    if (req.RequestedBy.UserId.HasValue) data.SetProperty("U_RequestedByUserId", req.RequestedBy.UserId.Value);
                    if (!string.IsNullOrWhiteSpace(req.RequestedBy.Email)) data.SetProperty("U_RequestedByEmail", req.RequestedBy.Email.Trim());
                }

                // Estat inicial (regla extranet)
                data.SetProperty("U_Status", "PENDING");

                // Lines
                lines = data.Child(LTable);
                if (req.Lines != null)
                {
                    foreach (var l in req.Lines)
                    {
                        if (l == null) continue;
                        if (string.IsNullOrWhiteSpace(l.Entity)) continue;
                        if (string.IsNullOrWhiteSpace(l.Field)) continue;

                        var ln = lines.Add();
                        ln.SetProperty("U_RequestId", 0);
                        ln.SetProperty("U_Entity", l.Entity.Trim());
                        ln.SetProperty("U_Field", l.Field.Trim());
                        ln.SetProperty("U_OldValue", (l.OldValue ?? "").Trim());
                        ln.SetProperty("U_NewValue", (l.NewValue ?? "").Trim());
                        ln.SetProperty("U_IsSensitive", l.IsSensitive ? "1" : "0");
                        ln.SetProperty("U_LineStatus", "Pending");
                        ln.SetProperty("U_LineError", "");
                    }
                }

                generalService.Add(data);
                return (true, "OK", "ChangeRequest creat (PENDING).", requestRef);
            }
            catch (Exception ex)
            {
                return (false, "EXCEPTION", ex.Message, null);
            }
            finally
            {
                if (lines != null) Marshal.ReleaseComObject(lines);
                if (data != null) Marshal.ReleaseComObject(data);
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

        public (bool ok, string code, string message) UpdateLines(ApiKeyProfile profile, string requestRef, ChangeRequestUdoUpsertRequest req)
        {
            if (profile == null) return (false, "MISSING_PROFILE", "Falta perfil (X-Api-Key).");
            if (string.IsNullOrWhiteSpace(requestRef)) return (false, "MISSING_REQUESTREF", "Falta requestRef.");
            if (req == null) return (false, "MISSING_BODY", "Falta body.");

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
                key.SetProperty("Code", requestRef.Trim());

                data = generalService.GetByParams(key);

                string status = "";
                try { status = (data.GetProperty("U_Status")?.ToString() ?? "").Trim(); } catch { }

                if (!status.Equals("PENDING", StringComparison.OrdinalIgnoreCase))
                    return (false, "NOT_PENDING", "No es pot modificar: l'estat no és PENDING.");

                // REGLA: extranet només pot tocar línies → substituïm totes les línies
                lines = data.Child(LTable);

                // Buida col·lecció
                try
                {
                    while (lines.Count > 0)
                        lines.Remove(0);
                }
                catch { /* si DI-API no deixa Remove, ho veurem al test */ }

                if (req.Lines != null)
                {
                    foreach (var l in req.Lines)
                    {
                        if (l == null) continue;
                        if (string.IsNullOrWhiteSpace(l.Entity)) continue;
                        if (string.IsNullOrWhiteSpace(l.Field)) continue;

                        var ln = lines.Add();
                        ln.SetProperty("U_RequestId", 0);
                        ln.SetProperty("U_Entity", l.Entity.Trim());
                        ln.SetProperty("U_Field", l.Field.Trim());
                        ln.SetProperty("U_OldValue", (l.OldValue ?? "").Trim());
                        ln.SetProperty("U_NewValue", (l.NewValue ?? "").Trim());
                        ln.SetProperty("U_IsSensitive", l.IsSensitive ? "1" : "0");
                        ln.SetProperty("U_LineStatus", "Pending");
                        ln.SetProperty("U_LineError", "");
                    }
                }

                generalService.Update(data);
                return (true, "OK", "Línies actualitzades (PENDING).");
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
                        int tid=0;
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
                    int tid=0;
                    if (v != null && int.TryParse(Convert.ToString(v), out tid)) item.TargetId = tid;
                }
                catch { }

                try
                {
                    var v = rsH.Fields.Item("U_RequestedByUserId").Value;
                    int uid=0;
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



        private static (bool ok, string? err) ConnectCompany(ApiKeyProfile profile, out Company? company)
        {
            company = null;

            string db = (profile.CompanyDb ?? "").Trim();
            string su = (profile.SapUser ?? "").Trim();
            string sp = (profile.SapPassword ?? "").Trim();

            if (string.IsNullOrWhiteSpace(db)) return (false, "DB_CONTEXT_MISSING");
            if (string.IsNullOrWhiteSpace(su) || string.IsNullOrWhiteSpace(sp)) return (false, "SAP_CONNECT_CONFIG_MISSING");

            if (string.IsNullOrWhiteSpace(ApplicationSettings.MSSQL_SRV) ||
                string.IsNullOrWhiteSpace(ApplicationSettings.MSSQL_USER) ||
                string.IsNullOrWhiteSpace(ApplicationSettings.MSSQL_PWD))
            {
                return (false, "MSSQL_CONFIG_MISSING");
            }

            var c = new Company
            {
                Server = ApplicationSettings.MSSQL_SRV,
                UseTrusted = false,
                UserName = su,
                Password = sp,
                language = BoSuppLangs.ln_Spanish,
                DbServerType = BoDataServerTypes.dst_MSSQL2019,
                CompanyDB = db,
                DbUserName = ApplicationSettings.MSSQL_USER,
                DbPassword = ApplicationSettings.MSSQL_PWD
            };

            int rc = c.Connect();
            if (rc == 0)
            {
                company = c;
                return (true, null);
            }

            string err = c.GetLastErrorDescription();
            try { Marshal.ReleaseComObject(c); } catch { }
            return (false, string.IsNullOrWhiteSpace(err) ? "SAP_CONNECT_FAILED" : err);
        }

        private static string BuildRequestRef()
        {
            string ts = DateTime.UtcNow.ToString("yyyyMMdd-HHmm");
            string rnd = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
            return $"CRQ-{ts}-{rnd}";
        }

        public (bool ok, string code, string message, string? requestRef) UpdateLinesOnly(string requestRef, List<ChangeLineDto> lines)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(requestRef))
                    return (false, "MISSING_REQUESTREF", "Falta requestRef.", null);

                if (lines == null || lines.Count == 0)
                    return (false, "MISSING_LINES", "Falten línies.", requestRef);

                // Resolve DocEntry via RequestRef
                int docEntry = 0;
                var rs = (SAPbobsCOM.Recordset)Functions.Dades.oCompany.GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
                try
                {
                    string rr = requestRef.Replace("'", "''");
                    rs.DoQuery($"SELECT TOP 1 DocEntry FROM [@XN_CRQ] WHERE U_RequestRef = '{rr}'");
                    if (rs.RecordCount <= 0) return (false, "NOT_FOUND", "No existeix requestRef.", requestRef);
                    docEntry = Convert.ToInt32(rs.Fields.Item(0).Value);
                }
                finally { System.Runtime.InteropServices.Marshal.ReleaseComObject(rs); }

                var companyService = Functions.Dades.oCompany.GetCompanyService();
                var generalService = companyService.GetGeneralService("XN_CRQ");

                var p = (SAPbobsCOM.GeneralDataParams)generalService.GetDataInterface(SAPbobsCOM.GeneralServiceDataInterfaces.gsGeneralDataParams);
                p.SetProperty("DocEntry", docEntry);

                var data = generalService.GetByParams(p);

                string status = Convert.ToString(data.GetProperty("U_Status"))?.Trim().ToUpperInvariant() ?? "";
                if (status != "PENDING")
                    return (false, "NOT_PENDING", "Només es pot modificar si està PENDING.", requestRef);

                // Replace lines
                var child = data.Child("XN_CRQ1");

                for (int i = child.Count - 1; i >= 0; i--)
                    child.Remove(i);

                foreach (var l in lines)
                {
                    var ln = child.Add();
                    ln.SetProperty("U_RequestId", docEntry); // acord: RequestId = DocEntry
                    ln.SetProperty("U_Entity", (l.Entity ?? "").Trim());
                    ln.SetProperty("U_Field", (l.Field ?? "").Trim());
                    ln.SetProperty("U_OldValue", (l.OldValue ?? "").Trim());
                    ln.SetProperty("U_NewValue", (l.NewValue ?? "").Trim());
                    ln.SetProperty("U_IsSensitive", l.IsSensitive ? "1" : "0");
                    ln.SetProperty("U_LineStatus", "PENDING");
                    ln.SetProperty("U_LineError", "");
                }

                generalService.Update(data);
                return (true, "OK", "Línies actualitzades (PENDING).", requestRef);
            }
            catch (Exception ex)
            {
                return (false, "EXCEPTION", ex.Message, requestRef);
            }
        }
    }
}



