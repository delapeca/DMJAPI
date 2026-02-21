using Microsoft.Data.SqlClient;
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
    /// Extranet: Create + UpdateLines (nomÃ©s si Status=PENDING)
    /// ConnexiÃ³ DI-API amb usuari tÃ¨cnic resolt per X-Api-Key (perfil), sense Dades.oCompany.
    /// </summary>
    public sealed class SapChangeRequestsUdoService
    {
        // UDO/UDT names (sense @) â€” segons EnsureSchema (XN_CRQ + XN_CRQ1)
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

            // IdempotÃ¨ncia: si ja existeix una CRQ PENDING equivalent, no en creem una de nova
            {
                var (found, existingRef, errCode, errMsg) = FindExistingPending(profile, req.CardCode.Trim(), req.Kind.Trim(), action, req.TargetId);
                if (errCode != null)
                    return (false, errCode, errMsg ?? "Error cercant pending.", null);

                if (found && !string.IsNullOrWhiteSpace(existingRef))
                    return (true, "OK_ALREADY_PENDING", "Ja existeix una peticiÃ³ PENDING equivalent. Es reutilitza.", existingRef);
            }

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
                    return (false, "NOT_PENDING", "No es pot modificar: l'estat no Ã©s PENDING.");

                // REGLA: extranet nomÃ©s pot tocar lÃ­nies â†’ substituÃ¯m totes les lÃ­nies
                lines = data.Child(LTable);

                // Buida colÂ·lecciÃ³
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
                return (true, "OK", "LÃ­nies actualitzades (PENDING).");
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

        public (bool ok, string code, string message, ChangeRequestListResponse? data) List(ApiKeyProfile profile, string cardCode, string? kind, int? targetId, string? status, int skip, int top, bool orderDesc)
        {
            if (profile == null) return (false, "MISSING_PROFILE", "Falta perfil (ProfileApiKey / X-Api-Key).", null);
            if (string.IsNullOrWhiteSpace(cardCode)) return (false, "MISSING_CARDCODE", "Falta cardCode.", null);

            if (skip < 0) skip = 0;
            if (top <= 0) top = 50;
            if (top > 200) top = 200;

            // IMPORTANT: ara NO fem DI-API. Llistat via SQL directe a la DB del perfil.
            string db = (profile.CompanyDb ?? "").Trim();
            if (string.IsNullOrWhiteSpace(db))
                return (false, "DB_CONTEXT_MISSING", "Falta CompanyDb al perfil (ApiKeyProfilesOptions).", null);

            if (string.IsNullOrWhiteSpace(ApplicationSettings.MSSQL_SRV) ||
                string.IsNullOrWhiteSpace(ApplicationSettings.MSSQL_USER) ||
                string.IsNullOrWhiteSpace(ApplicationSettings.MSSQL_PWD))
            {
                return (false, "MSSQL_CONFIG_MISSING", "Falta config MSSQL (SRV/USER/PWD).", null);
            }

            string connStr =
                $"Data Source={ApplicationSettings.MSSQL_SRV};" +
                $"Initial Catalog={db};" +
                $"User ID={ApplicationSettings.MSSQL_USER};" +
                $"Password={ApplicationSettings.MSSQL_PWD};" +
                $"TrustServerCertificate=True;";

            string cc = cardCode.Trim();
            string? kind2 = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim();
            string? status2 = string.IsNullOrWhiteSpace(status) ? null : status.Trim();

            int from = skip + 1;
            int to = skip + top;

            string order = orderDesc ? "DESC" : "ASC";

            // WHERE parametritzat (evitem injeccions)
            var where = new System.Text.StringBuilder();
            where.Append("T.U_CardCode = @cardCode");

            if (!string.IsNullOrWhiteSpace(kind2))
                where.Append(" AND T.U_Kind = @kind");

            if (targetId.HasValue)
                where.Append(" AND T.U_TargetId = @targetId");

            if (!string.IsNullOrWhiteSpace(status2))
                where.Append(" AND T.U_Status = @status");

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
                WHERE X.RN BETWEEN @from AND @to
                ORDER BY X.RN;";

            try
            {
                var resp = new ChangeRequestListResponse
                {
                    Ok = true,
                    Code = "OK",
                    Message = "OK",
                    CardCode = cc,
                    Items = new System.Collections.Generic.List<ChangeRequestListItemDto>(),
                    Paging = null
                };

                using var con = new SqlConnection(connStr);
                using var cmd = new SqlCommand(sql, con);

                cmd.Parameters.AddWithValue("@cardCode", cc);
                if (!string.IsNullOrWhiteSpace(kind2)) cmd.Parameters.AddWithValue("@kind", kind2!);
                if (targetId.HasValue) cmd.Parameters.AddWithValue("@targetId", targetId.Value);
                if (!string.IsNullOrWhiteSpace(status2)) cmd.Parameters.AddWithValue("@status", status2!);
                cmd.Parameters.AddWithValue("@from", from);
                cmd.Parameters.AddWithValue("@to", to);

                con.Open();
                using var r = cmd.ExecuteReader();

                int count = 0;

                while (r.Read())
                {
                    var item = new ChangeRequestListItemDto
                    {
                        RequestRef = (r["U_RequestRef"] as string)?.Trim(),
                        CardCode = (r["U_CardCode"] as string)?.Trim(),
                        Kind = (r["U_Kind"] as string)?.Trim(),
                        Action = (r["U_Action"] as string)?.Trim(),
                        Status = (r["U_Status"] as string)?.Trim(),
                        TargetName = (r["U_TargetName"] as string)?.Trim(),
                        RequestedAtUtc = (r["U_RequestedAtUtc"] as string)?.Trim(),
                        DecisionAtUtc = (r["U_DecisionAtUtc"] as string)?.Trim(),
                        DecisionNote = (r["U_DecisionNote"] as string)?.Trim(),
                        ApplyAtUtc = (r["U_ApplyAtUtc"] as string)?.Trim(),
                        ApplyError = (r["U_ApplyError"] as string)?.Trim(),
                    };

                    try
                    {
                        // U_TargetId pot venir null o numÃ¨ric
                        if (r["U_TargetId"] != null && r["U_TargetId"] != System.DBNull.Value)
                        {
                            if (int.TryParse(System.Convert.ToString(r["U_TargetId"]), out int tid))
                                item.TargetId = tid;
                        }
                    }
                    catch { /* ignore */ }

                    resp.Items.Add(item);
                    count++;
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
            catch (System.Exception ex)
            {
                return (false, "EXCEPTION", ex.Message, null);
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
                    int tid = 0;
                    if (v != null && int.TryParse(Convert.ToString(v), out tid)) item.TargetId = tid;
                }
                catch { }

                try
                {
                    var v = rsH.Fields.Item("U_RequestedByUserId").Value;
                    int uid = 0;
                    if (v != null && int.TryParse(Convert.ToString(v), out uid)) item.RequestedByUserId = uid;
                }
                catch { }

                string sqlL = $@"
                    SELECT
                      LineId,
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
                        LineId = Convert.ToInt32(rsL.Fields.Item("LineId").Value),

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

        public (bool ok, string code, string message) SetLineStatus(ApiKeyProfile profile, string cardCode, string code, int lineId, string lineStatus)
        {
            if (profile == null) return (false, "MISSING_PROFILE", "Falta perfil (ProfileApiKey / X-Api-Key).");
            if (string.IsNullOrWhiteSpace(cardCode)) return (false, "MISSING_CARDCODE", "Falta cardCode.");
            if (string.IsNullOrWhiteSpace(code)) return (false, "MISSING_CODE", "Falta code.");
            if (lineId < 0) return (false, "INVALID_LINEID", "lineId invÃ lid.");

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
                        int lid = 0;
                        if (v != null && int.TryParse(Convert.ToString(v), out lid))
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

                // ------------------------------------------------------
                // Recalcular estat de capÃ§alera segons estat de lÃ­nies
                // Regla:
                //   - VALIDATED si NO queda cap PENDING (poden existir REJECTED)
                //   - PENDING si existeix qualsevol PENDING
                // ------------------------------------------------------
                bool hasPending = false;

                for (int i = 0; i < lines.Count; i++)
                {
                    var ln = lines.Item(i);
                    string lst = "";
                    try { lst = (ln.GetProperty("U_LineStatus")?.ToString() ?? "").Trim().ToUpperInvariant(); }
                    catch { }

                    if (lst == "PENDING")
                    {
                        hasPending = true;
                        break;
                    }
                }

                if (hasPending)
                    data.SetProperty("U_Status", "PENDING");
                else
                    data.SetProperty("U_Status", "VALIDATED");


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

        private static (bool found, string? requestRef, string? errCode, string? errMsg) FindExistingPending(ApiKeyProfile profile, string cardCode, string kind, string action, int? targetId)
        {
            // Mateixa validaciÃ³ de context que List()
            string db = (profile.CompanyDb ?? "").Trim();
            if (string.IsNullOrWhiteSpace(db))
                return (false, null, "DB_CONTEXT_MISSING", "Falta CompanyDb al perfil (ApiKeyProfilesOptions).");

            if (string.IsNullOrWhiteSpace(ApplicationSettings.MSSQL_SRV) ||
                string.IsNullOrWhiteSpace(ApplicationSettings.MSSQL_USER) ||
                string.IsNullOrWhiteSpace(ApplicationSettings.MSSQL_PWD))
            {
                return (false, null, "MSSQL_CONFIG_MISSING", "Falta config MSSQL (SRV/USER/PWD).");
            }

            string connStr =
                $"Data Source={ApplicationSettings.MSSQL_SRV};" +
                $"Initial Catalog={db};" +
                $"User ID={ApplicationSettings.MSSQL_USER};" +
                $"Password={ApplicationSettings.MSSQL_PWD};" +
                $"TrustServerCertificate=True;";

            // Busquem la CRQ mÃ©s recent (DocEntry desc) que coincideixi amb el â€œscopeâ€ i sigui PENDING
            // Retornem U_RequestRef (equivalent al Code perquÃ¨ tu el seteges igual)
            string sql = @"
                SELECT TOP 1
                  ISNULL(NULLIF(LTRIM(RTRIM(T.U_RequestRef)), ''), T.Code) AS RequestRef
                FROM [@XN_CRQ] T
                WHERE
                  T.U_Status = 'PENDING'
                  AND T.U_CardCode = @cardCode
                  AND T.U_Kind = @kind
                  AND T.U_Action = @action
                  AND (
                        (@targetIdIsNull = 1 AND (T.U_TargetId IS NULL))
                        OR
                        (@targetIdIsNull = 0 AND T.U_TargetId = @targetId)
                      )
                ORDER BY T.DocEntry DESC;";

            try
            {
                using var con = new SqlConnection(connStr);
                using var cmd = new SqlCommand(sql, con);

                cmd.Parameters.AddWithValue("@cardCode", cardCode);
                cmd.Parameters.AddWithValue("@kind", kind);
                cmd.Parameters.AddWithValue("@action", action);

                int isNull = targetId.HasValue ? 0 : 1;
                cmd.Parameters.AddWithValue("@targetIdIsNull", isNull);
                cmd.Parameters.AddWithValue("@targetId", targetId.HasValue ? targetId.Value : 0);

                con.Open();
                var o = cmd.ExecuteScalar();
                string? rr = (o == null || o == DBNull.Value) ? null : Convert.ToString(o)?.Trim();

                if (string.IsNullOrWhiteSpace(rr))
                    return (false, null, null, null);

                return (true, rr, null, null);
            }
            catch (Exception ex)
            {
                return (false, null, "EXCEPTION", ex.Message);
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
                    return (false, "MISSING_LINES", "Falten lÃ­nies.", requestRef);

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
                    return (false, "NOT_PENDING", "NomÃ©s es pot modificar si estÃ  PENDING.", requestRef);

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
                return (true, "OK", "LÃ­nies actualitzades (PENDING).", requestRef);
            }
            catch (Exception ex)
            {
                return (false, "EXCEPTION", ex.Message, requestRef);
            }
        }


        // ============================================================
        // APPLY (ProfileApiKey version)
        //   - Només permet Apply si capçalera U_Status=VALIDATED
        //   - Escriu resultat a la capçalera:
        //       * APPLIED => U_ApplyAtUtc informat i U_ApplyError buit
        //       * ERROR   => U_ApplyAtUtc buit i U_ApplyError amb missatge
        //   - Escriu resultat per línia a U_LineError (si cal)
        // ============================================================
        public (bool ok, string code, string message) Apply(ApiKeyProfile profile, string requestRef, string? cardCode)
        {
            if (profile == null) return (false, "MISSING_PROFILE", "Falta perfil.");
            if (string.IsNullOrWhiteSpace(requestRef)) return (false, "MISSING_REQUESTREF", "Falta requestRef.");

            Company? company = null;
            CompanyService? companyService = null;
            GeneralService? generalService = null;
            GeneralDataParams? key = null;
            GeneralData? data = null;
            GeneralDataCollection? lines = null;
            BusinessPartners? bp = null;

            try
            {
                var (okConn, err) = ConnectCompany(profile, out company);
                if (!okConn || company == null)
                    return (false, err ?? "SAP_CONNECT_FAILED", "No es pot connectar a SAP.");

                companyService = company.GetCompanyService();
                generalService = companyService.GetGeneralService(UdoCode);

                key = (GeneralDataParams)generalService.GetDataInterface(GeneralServiceDataInterfaces.gsGeneralDataParams);
                key.SetProperty("Code", requestRef.Trim());

                data = generalService.GetByParams(key);

                // 1) Només Apply si la capçalera està VALIDATED
                string status = (data.GetProperty("U_Status")?.ToString() ?? "").Trim();
                if (!status.Equals("VALIDATED", System.StringComparison.OrdinalIgnoreCase))
                    return (false, "NOT_VALIDATED", "El CRQ no està VALIDATED.");

                // 2) CardCode (seguretat): si ens passen cardCode, ha de coincidir amb el CRQ
                string cc = (data.GetProperty("U_CardCode")?.ToString() ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(cardCode))
                {
                    string ccParam = (cardCode ?? "").Trim();
                    if (!cc.Equals(ccParam, System.StringComparison.OrdinalIgnoreCase))
                        return (false, "NOT_FOUND", "No existeix CRQ amb aquest cardCode + requestRef.");
                }

                // 3) Preparem BP 1 cop
                bp = (BusinessPartners)company.GetBusinessObject(BoObjectTypes.oBusinessPartners);
                if (!bp.GetByKey(cc))
                    return (false, "BP_NOT_FOUND", "No existeix BusinessPartner.");

                // 4) Recorrem línies VALIDATED i provem d'aplicar-les una a una
                lines = data.Child(LTable);

                var errors = new System.Collections.Generic.List<string>();

                for (int i = 0; i < lines.Count; i++)
                {
                    var ln = lines.Item(i);

                    string ls = (ln.GetProperty("U_LineStatus")?.ToString() ?? "").Trim();
                    if (!ls.Equals("VALIDATED", System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    string field = (ln.GetProperty("U_Field")?.ToString() ?? "").Trim();
                    string newVal = (ln.GetProperty("U_NewValue")?.ToString() ?? "").Trim();

                    // netegem error de línia abans de provar
                    try { ln.SetProperty("U_LineError", ""); } catch { }

                    // Map de camps permesos (extranet)
                    bool supported =
                        field.Equals("phone_mobile", System.StringComparison.OrdinalIgnoreCase) ||
                        field.Equals("phone_fixed", System.StringComparison.OrdinalIgnoreCase) ||
                        field.Equals("email", System.StringComparison.OrdinalIgnoreCase);

                    if (!supported)
                    {
                        string msg = $"FIELD_NOT_SUPPORTED: {field}";
                        errors.Add(msg);
                        try { ln.SetProperty("U_LineError", msg); } catch { }
                        continue;
                    }

                    // Apliquem el valor al BP
                    if (field.Equals("phone_mobile", System.StringComparison.OrdinalIgnoreCase))
                        bp.Cellular = newVal;

                    if (field.Equals("phone_fixed", System.StringComparison.OrdinalIgnoreCase))
                        bp.Phone1 = newVal;

                    if (field.Equals("email", System.StringComparison.OrdinalIgnoreCase))
                        bp.EmailAddress = newVal;

                    // Persistim a SAP (per línia) per poder registrar errors per línia
                    int rc = bp.Update();
                    if (rc != 0)
                    {
                        company.GetLastError(out int errCode, out string errMsg);
                        string msg = $"SAP_{errCode}: {(errMsg ?? "Error SAP.")}";
                        errors.Add(msg);
                        try { ln.SetProperty("U_LineError", msg); } catch { }
                        continue;
                    }

                    // OK: línia aplicada (U_LineError ja està buit)
                }

                // 5) Resultat capçalera: APPLIED / ERROR (via U_ApplyAtUtc + U_ApplyError)
                if (errors.Count == 0)
                {
                    data.SetProperty("U_ApplyAtUtc", System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                    data.SetProperty("U_ApplyError", "");
                    generalService.Update(data);
                    return (true, "OK_APPLIED", "Apply executat correctament (APPLIED).");
                }
                else
                {
                    // ERROR: guardem un resum a capçalera
                    string errSummary = string.Join(" | ", errors);
                    data.SetProperty("U_ApplyAtUtc", "");
                    data.SetProperty("U_ApplyError", errSummary);
                    generalService.Update(data);

                    return (false, "APPLY_ERROR", "Apply amb errors. Revisa U_ApplyError i U_LineError.");
                }
            }
            catch (System.Exception ex)
            {
                // Si peta per excepció, intentem deixar traça mínima si tenim data
                try
                {
                    if (data != null && generalService != null)
                    {
                        data.SetProperty("U_ApplyAtUtc", "");
                        data.SetProperty("U_ApplyError", ex.Message ?? "EXCEPTION");
                        generalService.Update(data);
                    }
                }
                catch { /* ignore */ }

                return (false, "EXCEPTION", ex.Message);
            }
            finally
            {
                if (bp != null) Marshal.ReleaseComObject(bp);
                if (lines != null) Marshal.ReleaseComObject(lines);
                if (data != null) Marshal.ReleaseComObject(data);
                if (key != null) Marshal.ReleaseComObject(key);
                if (generalService != null) Marshal.ReleaseComObject(generalService);
                if (companyService != null) Marshal.ReleaseComObject(companyService);

                if (company != null)
                {
                    try
                    {
                        if (company.Connected) company.Disconnect();
                        Marshal.ReleaseComObject(company);
                    }
                    catch { /* ignore */ }
                }
            }
        }

    }
}




