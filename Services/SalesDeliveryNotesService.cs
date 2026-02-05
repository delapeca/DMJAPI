using System;
using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    public class SalesDeliveryNotesService
    {
        private void EnsureConnection()
        {
            Dades.SetupDades();
        }

        // billingStatus: NOT_INVOICED | INVOICED
        public string GetSalesDeliveryNotesSummary(string cardCode, DateTime fromDate, DateTime toDate, string billingStatus)
        {
            EnsureConnection();

            billingStatus = (billingStatus ?? "").Trim().ToUpperInvariant();
            if (billingStatus != "INVOICED" && billingStatus != "NOT_INVOICED")
                billingStatus = "NOT_INVOICED";

            string[] parametres = new string[]
            {
                $"CardCode:{cardCode}",
                $"FromDate:{fromDate:yyyy-MM-dd}",
                $"ToDate:{toDate:yyyy-MM-dd}",
                $"BillingStatus:{billingStatus}"
            };

            string query = Funcions.GetQuery("GetSalesDeliveryNotesSummary.sql");
            return DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);
        }

        public string GetSalesDeliveryNoteDetail(string cardCode, int docEntry)
        {
            EnsureConnection();

            string[] parametres = new string[]
            {
                $"CardCode:{cardCode}",
                $"DocEntry:{docEntry}"
            };

            string query = Funcions.GetQuery("GetSalesDeliveryNoteDetail.sql");
            return DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);
        }
    }
}


