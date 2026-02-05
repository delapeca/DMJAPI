using System;
using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    /// <summary>
    /// Servei de negoci per treballar amb factures de venda (OINV).
    ///
    /// - NO conté lògica d'HTTP ni de controladors.
    /// - Llegeix els .sql incrustats amb Funcions.GetQuery().
    /// - Retorna sempre JSON en forma de string (GetJSon).
    /// </summary>
    public class SalesInvoicesService
    {
        /// <summary>
        /// Resum de factures de venda per un client (CardCode) en un rang de dates.
        ///
        /// Cada fila del JSON correspon a una factura:
        ///   - DocEntry / DocNum
        ///   - DocDate
        ///   - CardCode / CardName
        ///   - NumAtCard
        ///   - DocTotal / DocCur
        ///   - DocStatus / CANCELED / DocStatusText
        ///
        /// Es poden filtrar opcionalment:
        ///   - DocNum (exacte)
        ///   - NumAtCard (LIKE)
        /// </summary>
        public string GetSalesInvoicesSummary(
            string cardCode,
            DateTime fromDate,
            DateTime toDate,
            string docNum,
            string numAtCard)
        {
            EnsureConnection();

            // Preparem els paràmetres per al .sql
            string[] parametres =
            {
                $"CardCode:{cardCode}",
                $"FromDate:{fromDate:yyyy-MM-dd}",
                $"ToDate:{toDate:yyyy-MM-dd}",
                $"DocNum:{docNum ?? string.Empty}",
                $"NumAtCard:{numAtCard ?? string.Empty}"
            };

            // Llegim el .sql incrustat
            string query = Funcions.GetQuery("GetSalesInvoicesSummary.sql");

            // Recuperem el resultat en format JSON (array de files)
            string jsonResult = DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);

            // Si no hi ha cap fila, retornem un array buit
            if (string.IsNullOrEmpty(jsonResult))
            {
                return "[]";
            }

            return jsonResult;
        }

        /// <summary>
        /// Detall d'una factura de venda concreta (capçalera + línies).
        /// </summary>
        public string GetSalesInvoiceDetail(string cardCode, int docEntry)
        {
            EnsureConnection();

            string[] parametres =
            {
                $"CardCode:{cardCode}",
                $"DocEntry:{docEntry}"
            };

            string query = Funcions.GetQuery("GetSalesInvoiceDetail.sql");
            string jsonResult = DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);

            return string.IsNullOrEmpty(jsonResult) ? "[]" : jsonResult;
        }

        /// <summary>
        /// Inicialitza Dades.ConnectionStringDOMENJO si encara no està configurada.
        /// Mateix patró que SalesOrdersService / SalesOffersService.
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

