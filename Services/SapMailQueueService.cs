using SAPbobsCOM;
using System;
using System.Runtime.InteropServices;
using XNDmjApi.Functions;
using XNDmjApi.Models.MailQueue;

namespace XNDmjApi.Services
{
    public sealed class SapMailQueueService
    {
        // UDO code (tal com el vau crear)
        private const string UdoCode = "XN_MAILQ";

        public MailQueueEnqueueResponse Enqueue(string userToken, MailQueueEnqueueRequest req)
        {
            // Guards de context (mateix estil que SqlSchemaService)
            if (string.IsNullOrWhiteSpace(Dades.DOMENJO_BBDD))
                throw new Exception("DB_CONTEXT_MISSING");

            if (Dades.oCompany == null || !Dades.oCompany.Connected)
                throw new Exception("SAP_CONTEXT_MISSING");

            // Extra guard: assegurem CompanyDB = context DB
            try
            {
                var companyDb = (Dades.oCompany.CompanyDB ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(companyDb) &&
                    !string.Equals(companyDb, Dades.DOMENJO_BBDD.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("DB_CONTEXT_MISMATCH");
                }
            }
            catch (Exception ex)
            {
                // si ja és DB_CONTEXT_MISMATCH, re-throw
                if (ex.Message == "DB_CONTEXT_MISMATCH") throw;
            }

            // Code/Name (UDO MasterData) -> obligatori
            var code = NewCode();
            var nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            CompanyService? cs = null;
            GeneralService? gs = null;
            GeneralData? gd = null;

            try
            {
                cs = Dades.oCompany.GetCompanyService();
                gs = cs.GetGeneralService(UdoCode);
                gd = (GeneralData)gs.GetDataInterface(GeneralServiceDataInterfaces.gsGeneralData);

                // Claus UDO
                gd.SetProperty("Code", code);
                gd.SetProperty("Name", code);

                // UDFs (a SAP són U_<NomCamp>)
                gd.SetProperty("U_UserToken", userToken);

                gd.SetProperty("U_ObjType", (req.ObjType ?? "").Trim());
                gd.SetProperty("U_DocEntry", req.DocEntry);

                gd.SetProperty("U_ToEmail", req.ToEmail ?? "");
                gd.SetProperty("U_CcEmail", req.CcEmail ?? "");
                gd.SetProperty("U_BccEmail", req.BccEmail ?? "");

                gd.SetProperty("U_Subject", req.Subject ?? "");
                gd.SetProperty("U_Body", req.Body ?? "");

                gd.SetProperty("U_Template", req.Template ?? "");
                gd.SetProperty("U_Lang", req.Lang ?? "");

                gd.SetProperty("U_Status", "PENDING");
                gd.SetProperty("U_Retry", 0);
                gd.SetProperty("U_LastError", "");

                gd.SetProperty("U_RequestId", req.RequestId ?? "");
                gd.SetProperty("U_CreatedAt", nowIso);
                gd.SetProperty("U_SentAt", "");

                gs.Add(gd);

                return new MailQueueEnqueueResponse
                {
                    Ok = true,
                    Code = "OK",
                    Message = "ENQUEUED",
                    QueueCode = code,
                    ObjType = (req.ObjType ?? "").Trim(),
                    DocEntry = req.DocEntry,
                    Status = "PENDING",
                    CreatedAt = nowIso,
                    RequestId = req.RequestId
                };
            }
            catch (Exception ex)
            {
                throw new Exception("SAP_UDO_ADD_FAILED: " + ex.Message);
            }
            finally
            {
                TryReleaseCom(gd);
                TryReleaseCom(gs);
                TryReleaseCom(cs);
            }
        }

        private static string NewCode()
        {
            // SAP Code (50) -> fem GUID sense guions (32)
            return Guid.NewGuid().ToString("N").ToUpperInvariant();
        }

        private static void TryReleaseCom(object? o)
        {
            try
            {
                if (o != null && Marshal.IsComObject(o))
                    Marshal.ReleaseComObject(o);
            }
            catch { }
        }
    }
}

