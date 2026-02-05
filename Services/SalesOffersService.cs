using System;
using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    /// <summary>
    /// Servei de negoci per a ofertes de venda.
    /// 
    /// No té lògica de HTTP. Només:
    ///   - Muntar paràmetres
    ///   - Llegir el .sql incrustat
    ///   - Retornar el resultat en format JSON.
    /// </summary>
    public class SalesOffersService
    {
        private readonly Funcions _funcions = new Funcions();

        /// <summary>
        /// Retorna, en format JSON, el resum d’ofertes de venda
        /// per un client (CardCode), un rang de dates i filtres
        /// opcionals per estat, DocNum i NumAtCard.
        /// 
        /// Cada fila del JSON correspondrà a una oferta:
        ///   - DocEntry / DocNum
        ///   - DocDate / DocDueDate
        ///   - CardCode / CardName
        ///   - DocCur / DocTotal
        ///   - NumAtCard
        ///   - DocStatus / CANCELED / OfferStatusText
        /// </summary>
        public string GetSalesOffersSummary(string cardCode,DateTime fromDate,DateTime toDate,string status,string docNum,string numAtCard)
        {
            // Ens assegurem que la connexió DOMENJÓ està inicialitzada
            EnsureConnection();

            // Paràmetres per al .sql (format YYYY-MM-DD)
            string[] parametres =
            {
                $"CardCode:{cardCode}",
                $"FromDate:{fromDate:yyyy-MM-dd}",
                $"ToDate:{toDate:yyyy-MM-dd}",
                $"Status:{(string.IsNullOrWhiteSpace(status) ? "%" : status)}",
                $"DocNum:{docNum ?? string.Empty}",
                $"NumAtCard:{numAtCard ?? string.Empty}"
            };

            // Llegim el .sql incrustat
            string query = Funcions.GetQuery("GetSalesOffersSummary.sql");

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
        /// Retorna, en format JSON, el detall d'una oferta (OQUT + QUT1)
        /// per un client (CardCode) i un DocEntry concret.
        ///
        /// Cada fila del JSON correspon a UNA línia de l'oferta, amb
        /// les dades de capçalera repetides (mateix patró que INV1/OINV
        /// a GetItemPurchasedHistory).
        /// </summary>
        public string GetSalesOfferDetail(string cardCode, int docEntry)
        {
            EnsureConnection();

            string[] parametres =
            {
                $"CardCode:{cardCode}",
                $"DocEntry:{docEntry}"
            };

            string query = Funcions.GetQuery("GetSalesOfferDetail.sql");

            string jsonResult = DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);

            if (string.IsNullOrEmpty(jsonResult))
            {
                return "[]";
            }

            return jsonResult;
        }


        /// <summary>
        /// Inicialitza Dades.ConnectionStringDOMENJO si encara no està configurada.
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
