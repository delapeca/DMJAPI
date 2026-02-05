using Microsoft.VisualBasic;
using Newtonsoft.Json;
using SAPbobsCOM;
using System.ComponentModel.DataAnnotations;
using XNDmjApi.Functions;
using System.Data;
using System.Linq;
using Newtonsoft.Json;


namespace XNDmjApi.Services
{
    public class ItemsService
    {
        Funcions Funcions = new Funcions();

        // 🔹 Mètode antic per compatibilitat (ara delega al nou GetItemSales)
        //    → Qualsevol crida existent segueix funcionant igual.
        public string GetItem(string CardCode = "%", string ItemCode = "%", string ItmsGrpCod = "%")
        {
            // No filtrem per nom: passem ItemName = "%"
            return GetItemSales(CardCode, ItemCode, "%", ItmsGrpCod);
        }

        /// <summary>
        /// Retorna articles de venda (preus, descomptes, UoM) filtrant
        /// per client, codi, nom i grup d’articles.
        /// </summary>
        public string GetItemSales(
            string CardCode = "%",
            string ItemCode = "%",
            string ItemName = "%",
            string ItmsGrpCod = "%"
        )
        {
            // 🛠 Assegurem que la connexió a DOMENJÓ està inicialitzada
            EnsureConnection();

            // 1️⃣ Preparem els paràmetres per al SQL (GetItemsSalesPrices.sql)
            string[] parametres = {
                $"CardCode:{CardCode}",
                $"ItemCode:{ItemCode}",
                $"ItemName:{ItemName}",
                $"ItmsGrpCod:{ItmsGrpCod}"
            };

            string query = Funcions.GetQuery("GetItemsSalesPrices.sql");

            // 2️⃣ Recuperem les files en un DataTable (UNA fila per Article + UoM)
            DataTable dt = DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, query, parametres);

            if (dt == null || dt.Rows.Count == 0)
            {
                // Si no hi ha resultats, tornem un JSON buit coherent
                return "[]";
            }

            // 3️⃣ Agrupem per article (ItemCode, ItemName, grup, preus, etc.)
            var articles = dt.AsEnumerable()
                .GroupBy(row => new
                {
                    ItemCode = row.Field<string>("ItemCode"),
                    ItemName = row.Field<string>("ItemName"),

                    // smallint / int → fem servir Convert.ToInt32
                    ItmsGrpCod = row["ItmsGrpCod"] == DBNull.Value ? 0 : Convert.ToInt32(row["ItmsGrpCod"]),
                    ItmsGrpNam = row.Field<string>("ItmsGrpNam"),

                    // numèrics → Convert.ToDecimal
                    Price = row["Price"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Price"]),
                    Currency = row.Field<string>("Currency"),
                    Discount = row["Discount"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Discount"]),

                    OnHand = row["OnHand"] == DBNull.Value ? 0m : Convert.ToDecimal(row["OnHand"]),
                    Committed = row.Table.Columns.Contains("Committed") && row["Committed"] != DBNull.Value
                                    ? Convert.ToDecimal(row["Committed"])
                                    : 0m,
                    OnOrder = row["OnOrder"] == DBNull.Value ? 0m : Convert.ToDecimal(row["OnOrder"]),

                    // Pes (tal com ve d’OITM)
                    SWeight1 = row.Table.Columns.Contains("SWeight1") && row["SWeight1"] != DBNull.Value ? Convert.ToDecimal(row["SWeight1"]) : 0m,
                    SWeight1Unit = row.Table.Columns.Contains("SWeight1Unit") ? row.Field<string>("SWeight1Unit") : null,
                    SalUnitMsr = row.Table.Columns.Contains("SalUnitMsr") ? row.Field<string>("SalUnitMsr") : null,
                    InventoryUom = row.Table.Columns.Contains("InventoryUomCode")
                        ? row.Field<string>("InventoryUomCode")              // preferim el codi curt (ex: "ml")
                        : (row.Table.Columns.Contains("InvntryUom")
                            ? row.Field<string>("InvntryUom")                // fallback: el text que vingui d'OITM
                            : null),


                    ValidFor = row.Field<string>("validFor"),

                    UgpEntry = row["UgpEntry"] == DBNull.Value ? 0 : Convert.ToInt32(row["UgpEntry"]),
                    SUoMEntry = row["SUoMEntry"] == DBNull.Value ? 0 : Convert.ToInt32(row["SUoMEntry"]),

                    StdWhs = row.Field<string>("U_XN_StdMag")
                })
                .Select(g => new
                {
                    // 📦 Dades de l’article
                    itemCode = g.Key.ItemCode,
                    itemName = g.Key.ItemName,
                    itemGroupCode = g.Key.ItmsGrpCod,
                    itemGroupName = g.Key.ItmsGrpNam,
                    price = g.Key.Price,
                    currency = g.Key.Currency,
                    discount = g.Key.Discount,
                    onHand = g.Key.OnHand,
                    committed = g.Key.Committed,
                    onOrder = g.Key.OnOrder,
                    validFor = g.Key.ValidFor,
                    ugpEntry = g.Key.UgpEntry,
                    sUomEntry = g.Key.SUoMEntry,
                    standardWarehouse = g.Key.StdWhs,

                    // ⚖️ Pes per UM de venda (NO recalcularem per UoM)
                    weight = g.Key.SWeight1,
                    weightUnit = g.Key.SWeight1Unit,  // ex: "Kg"
                    salesUom = g.Key.SalUnitMsr,    // ex: "uni"
                    inventoryUom = g.Key.InventoryUom,  // ex: "ml"

                    // 🔹 Llista de UoM (units) per article
                    units = g.Select(row => new
                    {
                        uomEntry = row["UomEntry"] == DBNull.Value ? 0 : Convert.ToInt32(row["UomEntry"]),
                        uomCode = row.Field<string>("UomCode"),
                        uomName = row.Field<string>("UomName"),

                        baseQty = row["BaseQty"] == DBNull.Value ? 0m : Convert.ToDecimal(row["BaseQty"]),
                        altQty = row["AltQty"] == DBNull.Value ? 0m : Convert.ToDecimal(row["AltQty"]),

                        isDefaultSalesUom = (row["IsDefaultSalesUom"]?.ToString() == "Y")
                    }).ToList()
                })
                .ToList();

            // 4️⃣ Serialitzem a JSON i el retornem
            string jsonResult = JsonConvert.SerializeObject(articles);
            return jsonResult;
        }

        public string GetItem(string ItemCode = "%", string ItemName = "%", string ItmsGrpCod = "%", string ValidFor = "Y")
        {
            // 🛠 Assegurem que la connexió a DOMENJÓ està inicialitzada
            EnsureConnection();

            string[] parametres = {
                $"ItemCode:{ItemCode}",
                $"ItemName:{ItemName}",
                $"ItmsGrpCod:{ItmsGrpCod}",
                $"ValidFor:{ValidFor}"
            };

            string query = Funcions.GetQuery("GetItems.sql");
            string result = DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);

            return result;
        }

        private void EnsureConnection()
        {
            // Si la connexió a DOMENJÓ no està preparada, la muntem
            if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
            {
                Dades.DOMENJO_BBDD = "SBO_DOMENJO";  // el mateix que fas a CreateNewUser
                Dades.SetupDades();
            }
        }
    }
}
