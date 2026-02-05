using System;
using System.Data;
using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    /// <summary>
    /// Lectura d'adreces de client (CRD1) per self-service.
    /// IMPORTANT: Read-only. Mutacions NO aquí (DI-API / sol·licitud en fases posteriors).
    /// </summary>
    public class ClientAddressesService
    {
        private void EnsureConnection()
        {
            if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
            {
                Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                Dades.SetupDades();
            }
        }

        public DataTable GetAddresses(string cardCode)
        {
            if (string.IsNullOrWhiteSpace(cardCode))
                throw new ArgumentException("CardCode is required.", nameof(cardCode));

            EnsureConnection();

            var parametres = new[]
            {
                $"CardCode:{cardCode.Trim()}"
            };

            // CRD1: AdresType = 'B' (Bill To) / 'S' (Ship To)
            // IsDefault: fem servir OCRD.BillToDef / OCRD.ShipToDef
            var sql = @"
SELECT
    a.Address   AS AddressId,
    a.AdresType AS AddressType,
    a.Street,
    a.ZipCode,
    a.City,
    a.State     AS Province,
    a.Country,
    CASE
        WHEN (a.AdresType = 'B' AND a.Address = c.BillToDef) THEN 1
        WHEN (a.AdresType = 'S' AND a.Address = c.ShipToDef) THEN 1
        ELSE 0
    END AS IsDefault
FROM CRD1 a
INNER JOIN OCRD c ON c.CardCode = a.CardCode
WHERE a.CardCode = @CardCode
ORDER BY a.AdresType, a.Address;
";

            return DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, sql, parametres);
        }
    }
}
