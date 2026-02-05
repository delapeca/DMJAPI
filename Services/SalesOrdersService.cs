using System;
using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    /// <summary>
    /// Servei de negoci per treballar amb comandes de venda (ORDR).
    ///
    /// - NO conté lògica d'HTTP ni de controladors.
    /// - Llegeix els .sql incrustats amb Funcions.GetQuery().
    /// - Retorna sempre JSON en forma de string (GetJSon).
    /// </summary>
    public class SalesOrdersService
    {
        private readonly Funcions _funcions = new Funcions();

        /// <summary>
        /// Retorna, en format JSON, el resum de comandes de venda
        /// per un client (CardCode) entre un rang de dates.
        ///
        /// Cada fila del JSON correspon a una comanda:
        ///  - DocEntry / DocNum
        ///  - DocDate / DocDueDate
        ///  - CardCode / CardName
        ///  - NumAtCard
        ///  - DocTotal / DocCur
        ///  - DocStatus / CANCELED / OrderStatusText
        /// </summary>
        public string GetSalesOrdersSummary(string cardCode, DateTime fromDate, DateTime toDate, string status, string docNum, string numAtCard)
        {
            EnsureConnection();

            // Preparem els paràmetres per al .sql
            string[] parametres =
            {
                $"CardCode:{cardCode}",
                $"FromDate:{fromDate:yyyy-MM-dd}",
                $"ToDate:{toDate:yyyy-MM-dd}",
                $"Status:{status ?? "%"}",
                $"DocNum:{docNum ?? ""}",
                $"NumAtCard:{numAtCard ?? ""}"
            };

            // Llegim el .sql incrustat
            string query = Funcions.GetQuery("GetSalesOrdersSummary.sql");

            // Recuperem el resultat en format JSON (array de files)
            string jsonResult = DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);

            // Si no hi ha cap fila, retornem un array buit
            if (string.IsNullOrEmpty(jsonResult))
            {
                return "[]";
            }

            return jsonResult;
        }

        public string GetSalesOrderDetail(string cardCode, int docEntry)
        {
            EnsureConnection();

            string[] parametres =
            {
                $"CardCode:{cardCode}",
                $"DocEntry:{docEntry}"
            };

            string query = Funcions.GetQuery("GetSalesOrdersDetail.sql");
            string jsonResult = DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);

            return string.IsNullOrEmpty(jsonResult) ? "[]" : jsonResult;
        }

        /// <summary>
        /// Garanteix que la connexió DOMENJÓ està inicialitzada.
        /// Mateix patró que ItemsPurchasedService.
        /// </summary>
        private void EnsureConnection()
        {
            if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
            {
                Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                Dades.SetupDades();
            }
        }
    }

}
