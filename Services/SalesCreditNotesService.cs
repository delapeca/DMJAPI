using System;
using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    /// <summary>
    /// Servei per obtenir informació de crèdits de venda (abonaments, ORIN/RIN1).
    /// </summary>
    public class SalesCreditNotesService
    {
        private void EnsureConnection()
        {
            // Mateix patró que a SalesInvoicesService, SalesDeliveryNotesService, etc.
            if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
            {
                Dades.SetupDades();
            }
        }

        /// <summary>
        /// Resum de crèdits de venda (abonaments) per un client.
        ///
        /// SQL associat: Querys/GetSalesCreditNotesSummary.sql
        /// </summary>
        public string GetSalesCreditNotesSummary(
            string cardCode,
            string year = "",
            string fromDate = "",
            string toDate = ""
        )
        {
            EnsureConnection();

            string[] parametres = new string[]
            {
                $"CardCode:{cardCode}",
                $"Year:{year}",
                $"FromDate:{fromDate}",
                $"ToDate:{toDate}"
            };

            string query = Funcions.GetQuery("GetSalesCreditNotesSummary.sql");
            return DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);
        }

        /// <summary>
        /// Detall d'un crèdit de venda concret (capçalera + línies).
        ///
        /// SQL associat: Querys/GetSalesCreditNoteDetail.sql
        /// </summary>
        public string GetSalesCreditNoteDetail(string cardCode, int docEntry)
        {
            EnsureConnection();

            string[] parametres = new string[]
            {
                $"CardCode:{cardCode}",
                $"DocEntry:{docEntry}"
            };

            string query = Funcions.GetQuery("GetSalesCreditNoteDetail.sql");
            return DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);
        }
    }
}

