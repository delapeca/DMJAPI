using System;
using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    public class SalesReturnsService
    {
        private void EnsureConnection()
        {
            Dades.SetupDades();
        }

        public string GetSalesReturnsSummary(string cardCode, DateTime fromDate, DateTime toDate)
        {
            EnsureConnection();

            string[] parametres = new string[]
            {
                $"CardCode:{cardCode}",
                $"FromDate:{fromDate:yyyy-MM-dd}",
                $"ToDate:{toDate:yyyy-MM-dd}"
            };

            string query = Funcions.GetQuery("GetSalesReturnsSummary.sql");
            return DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);
        }

        public string GetSalesReturnDetail(string cardCode, int docEntry)
        {
            EnsureConnection();

            string[] parametres = new string[]
            {
                $"CardCode:{cardCode}",
                $"DocEntry:{docEntry}"
            };

            string query = Funcions.GetQuery("GetSalesReturnDetail.sql");
            return DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);
        }
    }
}


