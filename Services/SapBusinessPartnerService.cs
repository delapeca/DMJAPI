using SAPbobsCOM;
using System.Runtime.InteropServices;
using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    public class SapBusinessPartnerService
    {
        public (bool ok, string code, string message) UpdateBusinessPartner(
            string cardCode,
            string? phone1,
            string? cellular,
            string? email,
            string? contactPerson,
            string? fax,
            string? notes)
        {
            if (string.IsNullOrWhiteSpace(cardCode))
                return (false, "MISSING_CARDCODE", "Falta cardCode.");

            // Necessitem DI-API connectat (Dades.oCompany viu)
            var company = Dades.oCompany;
            if (company == null || !company.Connected)
                return (false, "SAP_NOT_CONNECTED", "No hi ha connexió DI-API (Dades.oCompany). Fes login SAP abans o revisa el servei.");

            BusinessPartners? bp = null;

            try
            {
                bp = (BusinessPartners)company.GetBusinessObject(BoObjectTypes.oBusinessPartners);

                if (!bp.GetByKey(cardCode.Trim()))
                    return (false, "BP_NOT_FOUND", $"No existeix el BusinessPartner '{cardCode}'.");

                // Apliquem només camps informats (patch parcial)
                if (phone1 != null)       bp.Phone1 = phone1;
                if (cellular != null)     bp.Cellular = cellular;

                // Propietat DI-API habitual: EmailAddress
                if (email != null)        bp.EmailAddress = email;

                if (contactPerson != null) bp.ContactPerson = contactPerson;
                if (fax != null)          bp.Fax = fax;
                if (notes != null)        bp.Notes = notes;

                int rc = bp.Update();
                if (rc == 0)
                    return (true, "OK", "BusinessPartner actualitzat correctament.");

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
