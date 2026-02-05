using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using SAPbobsCOM;
using System.Data;


namespace XNDmjApi.Functions
{
    public class PurcharseOrders
    {
        public string GetPuucharseOrders(string CardCode = "%", string Warehouse = "%")
        {
            //_connSQL = new SqlConnection(ConnectionStringDOMENJO);
            //_connSQL.Open();
            //string query = Funcions.GetQuery("GetPurchaseOrders.sql");
            //query += $"WHERE T0.CardCode like '{CardCode}' and Warehouse like '{Warehouse}'  group by T0.DocDate, T0.DocNum, T3.Warehouse, T0.CardCode, T0.CardName order by DocDate, DocNum;";
            //DataTable dt = new DataTable();
            //SqlCommand oCmd = new SqlCommand(query, _connSQL);
            //SqlDataReader oReader = oCmd.ExecuteReader();
            //if (oReader.HasRows)
            //{
            //    dt.Load(oReader);
            //}
            //oReader.Close();
            //_connSQL.Close();
            //return JsonConvert.SerializeObject(dt);

            

            string connectionString = Dades.ConnectionStringDOMENJO;
            string query = Funcions.GetQuery("GetPurchaseOrders.sql");
            //query += $"WHERE T0.CardCode like @CardCode and Warehouse like @Warehouse  group by T0.DocDate, T0.DocNum, T3.Warehouse, T0.CardCode, T0.CardName order by DocDate, DocNum;";

            List<SqlParameter> prm = new List<SqlParameter>();
            SqlParameter param = null;

            param = new SqlParameter("@CardCode", CardCode);
            //param = new SqlParameter("@ItemCode", ItemCode.HasValue ? (object)ItemCode : DBNull.Value);
            prm.Add(param);

            param = new SqlParameter("@Warehouse", Warehouse);
            //param = new SqlParameter("@ItemCode", ItemCode.HasValue ? (object)ItemCode : DBNull.Value);
            prm.Add(param);

            return DataAccess.DataTableToJSON(DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, query, prm));
        }

        public string GetPurcharseOrder( string CardCode, string Warehouse, int DocNum)
        {
            //_connSQL = new SqlConnection(ConnectionStringDOMENJO);
            //_connSQL.Open();
            //string query = Funcions.GetQuery("GetPurchaseOrder.sql");
            //query += $"WHERE T0.CardCode = '{CardCode}' and Warehouse = '{Warehouse}' and T0.DocNum={DocNum}  group by T0.[DocDate], T0.[DocNum], T3.Warehouse, T0.[CardCode], T0.[CardName], T1.[LineNum], T1.[ItemCode], T1.[Dscription], T1.[Quantity], T1.[OpenQty], T1.[Price], T1.[DiscPrcnt], T1.[LineTotal]  order by DocDate, DocNum;";
            //DataTable dt = new DataTable();
            //SqlCommand oCmd = new SqlCommand(query, _connSQL);
            //SqlDataReader oReader = oCmd.ExecuteReader();
            //if (oReader.HasRows)
            //{
            //    dt.Load(oReader);
            //}
            //oReader.Close();
            //_connSQL.Close();
            //return JsonConvert.SerializeObject(dt);

            Funcions funcions = new Funcions();

            string connectionString = Dades.ConnectionStringDOMENJO;
            string query = Funcions.GetQuery("GetPurchaseOrder.sql");
            query += $"WHERE T0.CardCode = @CardCode and Warehouse = @Warehouse and T0.DocNum=@DocNum  group by T0.[DocDate], T0.[DocNum], T3.Warehouse, T0.[CardCode], T0.[CardName], T1.[LineNum], T1.[ItemCode], T1.[Dscription], T1.[Quantity], T1.[OpenQty], T1.[Price], T1.[DiscPrcnt], T1.[LineTotal]  order by DocDate, DocNum;";

            List<SqlParameter> prm = new List<SqlParameter>();
            SqlParameter param = null;

            param = new SqlParameter("@CardCode", CardCode);
            //param = new SqlParameter("@ItemCode", ItemCode.HasValue ? (object)ItemCode : DBNull.Value);
            prm.Add(param);

            param = new SqlParameter("@Warehouse", Warehouse);
            //param = new SqlParameter("@ItemCode", ItemCode.HasValue ? (object)ItemCode : DBNull.Value);
            prm.Add(param);

            param = new SqlParameter("@DocNum", DocNum);
            //param = new SqlParameter("@ItemCode", ItemCode.HasValue ? (object)ItemCode : DBNull.Value);
            prm.Add(param);

            return DataAccess.DataTableToJSON(DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, query, prm));
        }
    }
}
