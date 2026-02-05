
using Microsoft.AspNetCore.Mvc;
using System;
using System.Globalization;
using XNDmjApi.Services;
using XNDmjApi.Functions;

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SalesOrdersController : ControllerBase
    {
        /// <summary>
        /// Resum de comandes de venda per un client (CardCode),
        /// filtrat per any o per rang de dates + estat + núm document.
        ///
        /// POST api/Orders/GetOrdersSummary
        ///
        /// Body (form-data / x-www-form-urlencoded):
        ///   cardCode  = codi de client SAP (OCRD.CardCode) [obligatori]
        ///   year      = any complet (p.ex. 2024) [opcional]
        ///   fromDate  = data inicial (YYYY-MM-DD) [opcional]
        ///   toDate    = data final   (YYYY-MM-DD) [opcional]
        ///   status    = OPEN / CLOSED / CANCELED / %           [opcional, per defecte "%"]
        ///   docNum    = núm. de comanda (ORDR.DocNum)          [opcional]
        ///   numAtCard = ref. client (ORDR.NumAtCard, amb LIKE) [opcional]
        ///
        /// Regles:
        ///   - Si arriba "year", agafem tot l’exercici.
        ///   - Si NO hi ha "year", exigim fromDate + toDate.
        /// </summary>
        [HttpPost("GetSalesOrdersSummary")]
        public ActionResult GetSalesOrdersSummary([FromForm] string cardCode,[FromForm] string year = "",[FromForm] string fromDate = "",[FromForm] string toDate = "",[FromForm] string status = "%",[FromForm] string docNum = "",[FromForm] string numAtCard = "")
        {
            if (string.IsNullOrWhiteSpace(cardCode))
            {
                return BadRequest("Falta el CardCode.");
            }

            // 1️⃣ Calculem el rang de dates (mateix patró que ItemsController)
            DateTime from;
            DateTime to;

            if (!string.IsNullOrWhiteSpace(year))
            {
                if (!int.TryParse(year, out int any))
                {
                    return BadRequest("Any invàlid.");
                }

                from = new DateTime(any, 1, 1);
                to = new DateTime(any, 12, 31);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate))
                {
                    return BadRequest("Has d'indicar un any o bé fromDate + toDate.");
                }

                // Format esperat: YYYY-MM-DD
                if (!DateTime.TryParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out from))
                {
                    return BadRequest("fromDate amb format invàlid. Usa YYYY-MM-DD.");
                }

                if (!DateTime.TryParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out to))
                {
                    return BadRequest("toDate amb format invàlid. Usa YYYY-MM-DD.");
                }
            }

            // 2️⃣ Cridem el servei de negoci
            var svc = new SalesOrdersService();

            string jsonResult = svc.GetSalesOrdersSummary(
                cardCode,
                from,
                to,
                status ?? "%",
                docNum ?? "",
                numAtCard ?? ""
            );

            // 3️⃣ Retornem el JSON en brut (mateix patró que ItemsController)
            if (jsonResult == null)
                return BadRequest("Error generant el JSON.");

            // Retorna el JSON “en brut” (array/object), no un string serialitzat
            return Content(jsonResult, "application/json");

        }

        [HttpPost("GetSalesOrderDetail")]
        public ActionResult GetSalesOrderDetail([FromForm] string cardCode, [FromForm] int docEntry)
        {
            if (string.IsNullOrWhiteSpace(cardCode))
                return BadRequest("Falta cardCode.");

            if (docEntry <= 0)
                return BadRequest("docEntry invàlid.");

            var svc = new SalesOrdersService();
            string jsonResult = svc.GetSalesOrderDetail(cardCode, docEntry);

            return Ok(jsonResult);
        }

    }
}
