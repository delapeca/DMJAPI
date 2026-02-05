using System;
using System.Data;
using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    /// <summary>
    /// Lectura de contactes de client (OCPR) per self-service.
    /// IMPORTANT: Read-only. Mutacions NO aquí (DI-API en fases posteriors).
    /// </summary>
    public class ClientContactsService
    {
        private void EnsureConnection()
        {
            if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
            {
                Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                Dades.SetupDades();
            }
        }

        public DataTable GetContacts(string cardCode)
        {
            if (string.IsNullOrWhiteSpace(cardCode))
                throw new ArgumentException("CardCode is required.", nameof(cardCode));

            EnsureConnection();

            var parametres = new[]
            {
                $"CardCode:{cardCode.Trim()}"
            };

            // OCPR: contactes d'empresa
            // Nota: a SAP B1 el camp de mòbil acostuma a ser "Cellolar" (sí, amb 'a')
            var sql = @"
SELECT
    p.CntctCode AS Id,
    p.Name      AS Name,
    p.Position  AS Role,
    p.E_MailL   AS Email,
    p.Tel1      AS Phone,
    p.Cellolar  AS Mobile,
    CASE WHEN ISNULL(p.Active,'Y') = 'Y' THEN 1 ELSE 0 END AS Active
FROM OCPR p
WHERE p.CardCode = @CardCode
ORDER BY p.Name;
";

            return DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, sql, parametres);
        }
    }
}
