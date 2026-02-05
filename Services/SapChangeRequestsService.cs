using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    public class SapChangeRequestsService
    {
        // UDO/UDT names (sense @)
        private const string UdoCode = "DMJ_CRQ";
        private const string LTable  = "DMJ_CRQ1";

        public class CreateContactChangeRequestInput
        {
            public string CardCode { get; set; } = "";
            public string Action { get; set; } = ""; // "create" | "update"
            public int? TargetId { get; set; }        // OCPR.CntctCode (si update)
            public string? Name { get; set; }
            public string? Address { get; set; }
            public string? Tel1 { get; set; }
            public string? Tel2 { get; set; }
            public string? Cellular { get; set; }
            public string? E_MailL { get; set; }
            public string? FirstName { get; set; }
            public string? MiddleName { get; set; }
            public string? LastName { get; set; }

            // Checkbox client: rebre docs vendes?
            public bool? ReceiveSalesDocs { get; set; }

            public string? Reason { get; set; }
            public int? RequestedByUserId { get; set; }
            public string? RequestedByEmail { get; set; }
        }

        public (bool ok, string code, string message, string? requestRef, int? docEntry, int? docNum) CreateContactChangeRequest(CreateContactChangeRequestInput input)
        {
            if (input == null) return (false, "MISSING_BODY", "Falta body.", null, null, null);
            if (string.IsNullOrWhiteSpace(input.CardCode)) return (false, "MISSING_CARDCODE", "Falta CardCode.", null, null, null);
            if (string.IsNullOrWhiteSpace(input.Action)) return (false, "MISSING_ACTION", "Falta Action (create/update).", null, null, null);

            string action = input.Action.Trim().ToLowerInvariant();
            if (action != "create" && action != "update") return (false, "INVALID_ACTION", "Action ha de ser 'create' o 'update'.", null, null, null);

            if (action == "update" && (!input.TargetId.HasValue || input.TargetId.Value <= 0))
                return (false, "MISSING_TARGET_ID", "Per update cal TargetId (CntctCode).", null, null, null);

            var company = Dades.oCompany;
            if (company == null || !company.Connected)
                return (false, "SAP_NOT_CONNECTED", "No hi ha connexió DI-API (Dades.oCompany). Fes login SAP abans i torna-ho a provar.", null, null, null);

            CompanyService? companyService = null;
            GeneralService? generalService = null;
            GeneralData? data = null;
            GeneralDataCollection? lines = null;

            try
            {
                // RequestRef tipus: CR-YYYYMMDD-HHMM-AB12
                string requestRef = BuildRequestRef();

                companyService = company.GetCompanyService();
                generalService = companyService.GetGeneralService(UdoCode);
                data = (GeneralData)generalService.GetDataInterface(GeneralServiceDataInterfaces.gsGeneralData);

                // --- Capçalera (@DMJ_CRQ)
                data.SetProperty("U_CardCode", input.CardCode.Trim());
                data.SetProperty("U_Kind", "contact");
                data.SetProperty("U_Action", action);
                data.SetProperty("U_Status", "Pending");
                data.SetProperty("U_Reason", (object?)input.Reason ?? "");
                data.SetProperty("U_RequestedByUserId", (object?)input.RequestedByUserId ?? 0);
                data.SetProperty("U_RequestedByEmail", (object?)input.RequestedByEmail ?? "");
                data.SetProperty("U_RequestRef", requestRef);

                if (input.TargetId.HasValue) data.SetProperty("U_TargetId", input.TargetId.Value);
                if (!string.IsNullOrWhiteSpace(input.Name)) data.SetProperty("U_TargetName", input.Name);

                // Guardem “target” com a JSON textual (simple i robust)
                string targetJson = BuildTargetJson(input, action);
                data.SetProperty("U_TargetJson", targetJson);

                // --- Línies (@DMJ_CRQ1)
                lines = data.Child(LTable);

                // Helpers: per cada camp informat -> línia NewValue
                AddLine(lines, "contact", action, input.TargetId, "Name", input.Name);
                AddLine(lines, "contact", action, input.TargetId, "Address", input.Address);
                AddLine(lines, "contact", action, input.TargetId, "Tel1", input.Tel1);
                AddLine(lines, "contact", action, input.TargetId, "Tel2", input.Tel2);
                AddLine(lines, "contact", action, input.TargetId, "Cellolar", input.Cellular);  // ⚠️ camp SAP: "Cellolar" (com el teu llistat)
                AddLine(lines, "contact", action, input.TargetId, "E_MailL", input.E_MailL);

                if (action == "create")
                {
                    AddLine(lines, "contact", action, input.TargetId, "FirstName", input.FirstName);
                    AddLine(lines, "contact", action, input.TargetId, "MiddleName", input.MiddleName);
                    AddLine(lines, "contact", action, input.TargetId, "LastName", input.LastName);
                }

                // U_BOY_85_ECAT: el client NO el veu; només checkbox “vendes”
                // true => "vendes" ; false/null => "" (buit)
                if (input.ReceiveSalesDocs.HasValue)
                {
                    string v = input.ReceiveSalesDocs.Value ? "vendes" : "";
                    AddLine(lines, "contact", action, input.TargetId, "U_BOY_85_ECAT", v);
                }

                // Inserim document UDO
                GeneralDataParams added = generalService.Add(data);

                int? docEntry = null;
                int? docNum = null;

                try { docEntry = Convert.ToInt32(added.GetProperty("DocEntry")); } catch { }
                try { docNum = Convert.ToInt32(added.GetProperty("DocNum")); } catch { }

                return (true, "OK", "ChangeRequest creat (Pending).", requestRef, docEntry, docNum);
            }
            catch (Exception ex)
            {
                return (false, "EXCEPTION", ex.Message, null, null, null);
            }
            finally
            {
                // Alliberem COM si aplica
                if (lines != null) Marshal.ReleaseComObject(lines);
                if (data != null) Marshal.ReleaseComObject(data);
                if (generalService != null) Marshal.ReleaseComObject(generalService);
                if (companyService != null) Marshal.ReleaseComObject(companyService);
            }
        }

        private static void AddLine(GeneralDataCollection lines, string entity, string action, int? targetId, string field, string? newValue)
        {
            if (newValue == null) return; // “patch parcial”: només camps informats

            var ln = lines.Add();
            ln.SetProperty("U_Entity", entity);
if (targetId.HasValue) ln.SetProperty("U_TargetId", targetId.Value);
            ln.SetProperty("U_Field", field);
            ln.SetProperty("U_OldValue", ""); // es pot omplir més endavant (quan comparem amb OCPR real)
            ln.SetProperty("U_NewValue", newValue);
            ln.SetProperty("U_IsSensitive", "N");
            ln.SetProperty("U_LineStatus", "Pending");
            ln.SetProperty("U_LineError", "");
        }

        private static string BuildRequestRef()
        {
            string ts = DateTime.UtcNow.ToString("yyyyMMdd-HHmm");
            // 4 hex “random”
            string rnd = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
            return $"CR-{ts}-{rnd}";
        }

        private static string JsonEscape(string s)
        {
            return s
                .Replace("\\\\", "\\\\\\\\")
                .Replace("\"", "\\\\\"")
                .Replace("\r", "\\\\r")
                .Replace("\n", "\\\\n")
                .Replace("\t", "\\\\t");
        }

        private static string BuildTargetJson(CreateContactChangeRequestInput input, string action)
        {
            // JSON textual minimal (no depèn de Newtonsoft)
            // Guardem només el que ens han informat.
            var parts = new List<string>();
            parts.Add($"\"action\":\"{JsonEscape(action)}\"");
            if (input.TargetId.HasValue) parts.Add($"\"id\":{input.TargetId.Value}");

            void addStr(string key, string? val)
            {
                if (val == null) return;
                parts.Add($"\"{JsonEscape(key)}\":\"{JsonEscape(val)}\"");
            }

            addStr("name", input.Name);
            addStr("address", input.Address);
            addStr("tel1", input.Tel1);
            addStr("tel2", input.Tel2);
            addStr("cellular", input.Cellular);
            addStr("email", input.E_MailL);
            addStr("firstName", input.FirstName);
            addStr("middleName", input.MiddleName);
            addStr("lastName", input.LastName);

            if (input.ReceiveSalesDocs.HasValue)
                parts.Add($"\"receiveSalesDocs\":{(input.ReceiveSalesDocs.Value ? "true" : "false")}");

            return "{" + string.Join(",", parts) + "}";
        }
    }
}

