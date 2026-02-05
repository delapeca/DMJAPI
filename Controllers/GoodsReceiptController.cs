using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System.Data;
using XNDmjApi.Functions;
using XNDmjApi.Services;
using XNDmjApi.Models;

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GoodsReceiptController : ControllerBase
    {
        private readonly IConfiguration _config;
        string ConnectionStringDOMENJO = null;

        Funcions Funcions = new Funcions();
        GoodsReceiptService service = new GoodsReceiptService();
        SAPLoginService login = new SAPLoginService();

        SqlConnection _connSQL;

        public GoodsReceiptController(IConfiguration configuration)
        {
            _config = configuration;
            //ConnectionStringDOMENJO = _config["Logging:BBDD:MSSQL"];
        }

        // GET: api/<GetOrders>
        [HttpPost("GetOpenOrders")]
        public ActionResult GetOpenOrders([FromForm] string userToken, [FromForm] DateTime iniDate, [FromForm] DateTime endDate, [FromForm] string cardCode = "%", [FromForm] string warehouse = "%")
        {
            if (!login.ValidateUserToken(userToken)) return BadRequest("Toquen caducat");

            string query = Funcions.GetQuery("GetOpenPurchaseOrders.sql");
            query += $"WHERE T0.CardCode like '{cardCode}' and Warehouse like '{warehouse}' and T0.DocDate between '{iniDate.ToString("yyyy/MM/dd")}' and '{endDate.ToString("yyyy/MM/dd")}'  group by T0.DocDate, T0.DocNum, T3.Warehouse, T0.CardCode, T0.CardName order by DocDate, DocNum;";

            //List<SqlParameter> SqlParams = new List<SqlParameter>();
            //SqlParameter p = new SqlParameter();

            string[] parametres = { $"userToken:'{userToken}'", $"iniDate:'{iniDate}'", $"endDate:'{endDate}'", $"cardCode:'{cardCode}'", $"warehouse:'{warehouse}'" };
            //string[] parametres = null;

            //p = new SqlParameter(); p.ParameterName = "userToken"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = userToken; SqlParams.Add(p);
            //p = new SqlParameter(); p.ParameterName = "iniDate"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = iniDate; SqlParams.Add(p);
            //p = new SqlParameter(); p.ParameterName = "endDate"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = endDate; SqlParams.Add(p);
            //p = new SqlParameter(); p.ParameterName = "cardCode"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = cardCode; SqlParams.Add(p);
            //p = new SqlParameter(); p.ParameterName = "warehouse"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = warehouse; SqlParams.Add(p);

            var dt = DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, query, parametres);

            var result = JsonConvert.SerializeObject(dt);


            return Ok(result);
        }

        [HttpPost("GetOpenOrder")]
        public ActionResult GetOpenOrder([FromForm] string userToken, [FromForm] int docNum)
        {
            if (!login.ValidateUserToken(userToken)) return BadRequest("Toquen caducat");

            string query = Funcions.GetQuery("GetOpenPurchaseOrder.sql");
            query += $"WHERE T0.DocNum={docNum}  group by  T0.[DocDate], T0.[DocNum], T3.Warehouse, T0.[CardCode], T0.[CardName], T1.[LineNum], T1.[ItemCode], T1.[Dscription], T1.[Quantity], T1.[OpenQty], T1.[Price], T1.[DiscPrcnt], T1.[LineTotal]  order by DocDate, DocNum;";

            //List<SqlParameter> SqlParams = new List<SqlParameter>();
            //SqlParameter p = new SqlParameter();

            string[] parametres = { $"userToken:{userToken}", $"docNum:{docNum}" };
            //string[] parametres = null;

            //p = new SqlParameter(); p.ParameterName = "userToken"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = userToken; SqlParams.Add(p);
            //p = new SqlParameter(); p.ParameterName = "iniDate"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = iniDate; SqlParams.Add(p);
            //p = new SqlParameter(); p.ParameterName = "endDate"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = endDate; SqlParams.Add(p);
            //p = new SqlParameter(); p.ParameterName = "cardCode"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = cardCode; SqlParams.Add(p);
            //p = new SqlParameter(); p.ParameterName = "warehouse"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = warehouse; SqlParams.Add(p);

            var dt = DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, query, parametres);

            var result = JsonConvert.SerializeObject(dt);


            return Ok(result);
        }

        //[HttpPost("NewGoodsReceipt")]
        //public ActionResult SetGoodsReceipt([FromForm] string userToken, [FromForm] int docNum)
        //{
        //    //if (!login.ValidateUserToken(userToken)) return BadRequest("Toquen caducat");

        //    var result = service.setGoodsReceipt("ppp");
        //    return Ok(result);
        //}

        [HttpPost("SetTempGoodReceipt")]
        public ActionResult SetTempGoodReceipt([FromForm] string userToken, [FromForm] string jsonTempGoodReceipt)
        {
            if (!login.ValidateUserToken(userToken)) 
                return BadRequest("Toquen caducat");

            var result = service.setTemporalGoodReceipt(jsonTempGoodReceipt);

            return Ok(result);
            
        }

        [HttpPost("SetGoodReceipt")]
        public ActionResult SetGoodReceipt([FromForm] string userToken, [FromForm] string jsonGoodReceipt)
        {
            if (!login.ValidateUserToken(userToken)) return BadRequest("Toquen caducat");

            string result = service.setGoodsReceipt(jsonGoodReceipt);

            return Ok(result);

        }

        [HttpPost("GetBPWithOpenOrders")]
        public ActionResult GetBPWithOpenOrders([FromForm] string userToken, [FromForm] string warehouse)
        {
            if (!login.ValidateUserToken(userToken)) return BadRequest("Toquen caducat");
            
            string result = service.GetBPWithOpenOrders(warehouse);

            return Ok(result);

        }

        [HttpPost("GetOpenOrdersFromBP")]
        public ActionResult GetOpenOrdersFromBP([FromForm] string userToken, [FromForm] string warehouse, [FromForm] string cardCode)
        {
            if (!login.ValidateUserToken(userToken)) return BadRequest("Toquen caducat");

            string result = service.GetOpenOrdersFromBP(warehouse, cardCode);

            return Ok(result);

        }

        [HttpPost("GetOpenOrderFromBP")]
        public ActionResult GetOpenOrderFromBP([FromForm] string userToken, [FromForm] string warehouse, [FromForm] string cardCode, [FromForm] int docNum, [FromForm] int empID)
        {
            if (!login.ValidateUserToken(userToken)) return BadRequest("Toquen caducat");

            string result = service.GetOpenOrderFromBP(warehouse, cardCode, docNum, empID);

            return Ok(result);

        }

        [HttpPost("GetTempGoodReceipt")]
        public ActionResult GetTempGoodReceipt([FromForm] string userToken, [FromForm] string empID)
        {
            if (!login.ValidateUserToken(userToken)) return BadRequest("Toquen caducat");

            string jsonResult = service.GetTemporalGoodReceipt(empID);

            return Ok(jsonResult);

        }
    }
}
