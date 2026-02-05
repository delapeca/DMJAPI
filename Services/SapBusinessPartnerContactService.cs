using Microsoft.Data.SqlClient;
using SAPbobsCOM;
using System.Data;
using System.Runtime.InteropServices;
using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    public class SapBusinessPartnerContactService
    {
        private static string MapBoyumEmailCat(bool? receiveSalesDocs)
        {
            // IMPORTANT: el client NO veu U_BOY_85_ECAT.
            // receiveSalesDocs=true  => "vendes"
            // receiveSalesDocs=false => "" (buit)
            if (receiveSalesDocs == null) return null!;
            return receiveSalesDocs.Value ? "vendes" : "";
        }

        public (bool ok, string code, string message, int? contactId) CreateContact(
            string cardCode,
            string name,
            string? address,
            string? tel1,
            string? tel2,
            string? cellular,
            string? email,
            string? firstName,
            string? middleName,
            string? lastName,
            bool? receiveSalesDocs)
        {
            if (string.IsNullOrWhiteSpace(cardCode)) return (false, "MISSING_CARDCODE", "Falta cardCode.", null);
            if (string.IsNullOrWhiteSpace(name)) return (false, "MISSING_NAME", "Falta Name.", null);

            var company = Dades.oCompany;
            if (company == null || !company.Connected)
                return (false, "SAP_NOT_CONNECTED", "No hi ha connexió DI-API (Dades.oCompany).", null);

            BusinessPartners? bp = null;

            try
            {
                bp = (BusinessPartners)company.GetBusinessObject(BoObjectTypes.oBusinessPartners);

                if (!bp.GetByKey(cardCode.Trim()))
                    return (false, "BP_NOT_FOUND", $"No existeix el BusinessPartner '{cardCode}'.", null);

                // Afegim línia de contacte
                bp.ContactEmployees.Add();
                int newLine = bp.ContactEmployees.Count - 1;
                if (newLine < 0) newLine = 0;
                bp.ContactEmployees.SetCurrentLine(newLine);

                bp.ContactEmployees.Name = name;

                if (address != null) bp.ContactEmployees.Address = address;
                if (tel1 != null) bp.ContactEmployees.Phone1 = tel1;
                if (tel2 != null) bp.ContactEmployees.Phone2 = tel2;
                if (cellular != null) bp.ContactEmployees.MobilePhone = cellular;

                // DI-API sol exposar "E_Mail"
                if (email != null) bp.ContactEmployees.E_Mail = email;

                if (firstName != null) bp.ContactEmployees.FirstName = firstName;
                if (middleName != null) bp.ContactEmployees.MiddleName = middleName;
                if (lastName != null) bp.ContactEmployees.LastName = lastName;

                if (receiveSalesDocs != null)
                {
                    string v = MapBoyumEmailCat(receiveSalesDocs);
                    bp.ContactEmployees.UserFields.Fields.Item("U_BOY_85_ECAT").Value = v;
                }

                int rc = bp.Update();
                if (rc != 0)
                {
                    company.GetLastError(out int errCode, out string errMsg);
                    return (false, $"SAP_{errCode}", errMsg ?? "Error DI-API desconegut.", null);
                }

                // Intentem recuperar el CntctCode per SQL (OCPR)
                // (No exposem U_BOY_85_ECAT al client; això és intern)
                try
                {
                    if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
                    {
                        if (string.IsNullOrWhiteSpace(Dades.DOMENJO_BBDD))
                            Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                        Dades.SetupDades();
                    }

                    string sql = @"
SELECT TOP 1 CntctCode
FROM OCPR
WHERE CardCode = @CardCode
  AND Name = @Name
  AND (@Email IS NULL OR E_MailL = @Email)
ORDER BY CntctCode DESC;";

                    var prm = new List<SqlParameter>
                    {
                        new SqlParameter("@CardCode", SqlDbType.VarChar, 20) { Value = cardCode.Trim() },
                        new SqlParameter("@Name", SqlDbType.NVarChar, 100) { Value = name },
                        new SqlParameter("@Email", SqlDbType.NVarChar, 100) { Value = (object?)email ?? DBNull.Value },
                    };

                    DataTable dt = DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, sql, prm);
                    if (dt != null && dt.Rows.Count > 0 && dt.Columns.Contains("CntctCode"))
                    {
                        int id;
                        if (int.TryParse(dt.Rows[0]["CntctCode"]?.ToString(), out id))
                            return (true, "OK", "Contacte creat correctament.", id);
                    }
                }
                catch
                {
                    // si falla la lectura, no trenquem el create
                }

                return (true, "OK", "Contacte creat correctament.", null);
            }
            catch (System.Exception ex)
            {
                return (false, "EXCEPTION", ex.Message, null);
            }
            finally
            {
                if (bp != null) Marshal.ReleaseComObject(bp);
            }
        }

        public (bool ok, string code, string message) UpdateContact(
            string cardCode,
            int contactId,
            string? address,
            string? tel1,
            string? tel2,
            string? cellular,
            string? email,
            bool? receiveSalesDocs)
        {
            if (string.IsNullOrWhiteSpace(cardCode)) return (false, "MISSING_CARDCODE", "Falta cardCode.");
            if (contactId <= 0) return (false, "MISSING_ID", "Falta target.id (contactId).");

            var company = Dades.oCompany;
            if (company == null || !company.Connected)
                return (false, "SAP_NOT_CONNECTED", "No hi ha connexió DI-API (Dades.oCompany).");

            BusinessPartners? bp = null;

            try
            {
                bp = (BusinessPartners)company.GetBusinessObject(BoObjectTypes.oBusinessPartners);

                if (!bp.GetByKey(cardCode.Trim()))
                    return (false, "BP_NOT_FOUND", $"No existeix el BusinessPartner '{cardCode}'.");

                bool found = false;

                for (int i = 0; i < bp.ContactEmployees.Count; i++)
                {
                    bp.ContactEmployees.SetCurrentLine(i);
                    if (bp.ContactEmployees.InternalCode == contactId)
                    {
                        found = true;

                        // PATCH parcial: només camps permesos (update)
                        if (address != null) bp.ContactEmployees.Address = address;
                        if (tel1 != null) bp.ContactEmployees.Phone1 = tel1;
                        if (tel2 != null) bp.ContactEmployees.Phone2 = tel2;
                        if (cellular != null) bp.ContactEmployees.MobilePhone = cellular;
                        if (email != null) bp.ContactEmployees.E_Mail = email;

                        if (receiveSalesDocs != null)
                        {
                            string v = MapBoyumEmailCat(receiveSalesDocs);
                            bp.ContactEmployees.UserFields.Fields.Item("U_BOY_85_ECAT").Value = v;
                        }

                        break;
                    }
                }

                if (!found)
                    return (false, "CONTACT_NOT_FOUND", $"No existeix el contacte id={contactId} per BP '{cardCode}'.");

                int rc = bp.Update();
                if (rc == 0)
                    return (true, "OK", "Contacte actualitzat correctament.");

                company.GetLastError(out int errCode, out string errMsg);
                return (false, $"SAP_{errCode}", errMsg ?? "Error DI-API desconegut.");
            }
            catch (System.Exception ex)
            {
                return (false, "EXCEPTION", ex.Message);
            }
            finally
            {
                if (bp != null) Marshal.ReleaseComObject(bp);
            }
        }
    }
}
