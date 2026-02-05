using System;
using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    public class ItemsPurchasedService
    {
        private readonly Funcions _funcions = new Funcions();

        /// <summary>
        /// Retorna, en format JSON, el resum d’articles comprats
        /// per un client (CardCode) i un rang de dates.
        ///
        /// Cada fila del JSON correspon a un article:
        ///  - ItmsGrpCod / ItmsGrpNam
        ///  - SubGroupCode / SubGroupName (ara mateix NULL)
        ///  - ItemCode / ItemName
        ///  - TotalQuantity
        ///  - CurrentPrice / CurrentDiscount / CurrentNetPrice
        /// </summary>
        public string GetItemsPurchasedSummary(string cardCode, DateTime fromDate, DateTime toDate)
        {
            // Ens assegurem que la connexió DOMENJÓ està inicialitzada
            EnsureConnection();

            // Passem les dates en format YYYY-MM-DD perquè SQL Server les entengui bé
            string[] parametres =
            {
                $"CardCode:{cardCode}",
                $"FromDate:{fromDate:yyyy-MM-dd}",
                $"ToDate:{toDate:yyyy-MM-dd}"
            };

            // Llegim el .sql incrustat com fas amb la resta
            string query = Funcions.GetQuery("GetItemsPurchasedSummary.sql");

            // Recuperem el resultat en format JSON (array de files)
            string jsonResult = DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);

            // Si no hi ha cap fila, retornem un array buit
            if (string.IsNullOrEmpty(jsonResult))
            {
                return "[]";
            }

            return jsonResult;
        }

        public string GetItemPurchasedHistory(string cardCode, string itemCode, DateTime fromDate, DateTime toDate)
        {
            // Ens assegurem que la connexió DOMENJÓ està inicialitzada
            EnsureConnection();

            // Paràmetres per al .sql
            string[] parametres =
            {
            $"CardCode:{cardCode}",
            $"ItemCode:{itemCode}",
            $"FromDate:{fromDate:yyyy-MM-dd}",
            $"ToDate:{toDate:yyyy-MM-dd}"
            };

            // Llegim el .sql incrustat
            string query = Funcions.GetQuery("GetItemPurchasedHistory.sql");

            // Recuperem el resultat en format JSON (array de files)
            string jsonResult = DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);

            // Si no hi ha cap fila, retornem un array buit
            if (string.IsNullOrEmpty(jsonResult))
            {
                return "[]";
            }

            return jsonResult;
        }


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
