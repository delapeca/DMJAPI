using Microsoft.AspNetCore.Mvc;
using System;
using System.Globalization;
using XNDmjApi.Services;

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SalesOffersController : ControllerBase
    {
        /// <summary>
        /// Resum d’ofertes de venda per un client (CardCode),
        /// filtrat per any o per rang de dates, estat i opcionalment
        /// per número de document o referència de client.
        ///
        /// POST api/Offers/GetOffersSummary
        ///
        /// Body (form-data / x-www-form-urlencoded):
        ///   cardCode  = codi de client SAP (OCRD.CardCode) [obligatori]
        ///   year      = any, opcional (p.ex. 2024)
        ///   fromDate  = data inicial (YYYY-MM-DD), opcional
        ///   toDate    = data final   (YYYY-MM-DD), opcional
        ///   status    = Open / Closed / Cancelled / %  (totes)
        ///   docNum    = número d’oferta, opcional
        ///   numAtCard = referència de client, opcional
        ///
        /// Regles:
        ///   - Si arriba "year", fem servir tot l’exercici.
        ///   - Si no hi ha "year", fem servir fromDate + toDate.
        /// </summary>
        [HttpPost("GetSalesOffersSummary")]
        public ActionResult GetSalesOffersSummary([FromForm] string cardCode,[FromForm] string year = "",[FromForm] string fromDate = "",[FromForm] string toDate = "",[FromForm] string status = "%",[FromForm] string docNum = "",[FromForm] string numAtCard = "")
        {
            if (string.IsNullOrWhiteSpace(cardCode))
            {
                return BadRequest("Falta el CardCode.");
            }

            // 1️⃣ Calculem el rang de dates (mateix patró que ItemsPurchased)
            DateTime from;
            DateTime to;

            if (!string.IsNullOrWhiteSpace(year))
            {
                // Any complet
                if (!int.TryParse(year, out int any))
                {
                    return BadRequest("Any invàlid.");
                }

                from = new DateTime(any, 1, 1);
                to = new DateTime(any, 12, 31);
            }
            else
            {
                // Sense any → exigim fromDate + toDate
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

            // 2️⃣ Normalitzem status: buit → totes
            status = string.IsNullOrWhiteSpace(status) ? "%" : status;

            // 3️⃣ Cridem el servei de negoci
            var svc = new SalesOffersService();
            string jsonResult = svc.GetSalesOffersSummary(cardCode, from, to, status, docNum, numAtCard);

            // 4️⃣ Retornem el JSON, mateix patró que la resta
            if (jsonResult != null)
                return Ok(jsonResult);
            else
                return BadRequest(jsonResult);
        }

        /// <summary>
        /// Detall d'una oferta de venda (capçalera + línies),
        /// identificada per DocEntry i limitada al client (CardCode).
        ///
        /// POST api/Offers/GetOfferDetail
        ///
        /// Body (form-data):
        ///   cardCode = codi de client SAP (OCRD.CardCode)
        ///   docEntry = DocEntry de l'oferta (OQUT.DocEntry)
        /// </summary>
        [HttpPost("GetOfferDetail")]
        public ActionResult GetSalesOfferDetail([FromForm] string cardCode,[FromForm] string docEntry)
        {
            if (string.IsNullOrWhiteSpace(cardCode))
            {
                return BadRequest("Falta el CardCode.");
            }

            if (string.IsNullOrWhiteSpace(docEntry) || !int.TryParse(docEntry, out int docEntryInt))
            {
                return BadRequest("DocEntry invàlid.");
            }

            var svc = new SalesOffersService();
            string jsonResult = svc.GetSalesOfferDetail(cardCode, docEntryInt);

            if (jsonResult != null)
                return Ok(jsonResult);
            else
                return BadRequest(jsonResult);
        }


    }
}

