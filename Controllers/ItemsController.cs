using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using SAPbobsCOM;
using System.Globalization;
using XNDmjApi.Functions;
using XNDmjApi.Models;
using XNDmjApi.Services;


// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ItemsController : ControllerBase
    {
        
        // POST api/<ItemsController>
        [HttpPost("GetItems")]
        public ActionResult GetItems([FromForm]string itemCode="%", [FromForm] string itemName = "%", [FromForm] string itmsGrpCod = "%", [FromForm] string validFor = "Y")
        {
            //Clases.Log.LogWrite($"SetPassword: token={token}, encriptedOldPassword={encriptedOldPassword}, encriptedNewPassword={encriptedNewPassword}");
            ItemsService SItems = new ItemsService();
            string jsonResult = SItems.GetItem(itemCode, itemName, itmsGrpCod, validFor);
            

            if (jsonResult != null)
                return Ok(jsonResult);
            else
                return BadRequest(jsonResult);
        }


        // POST api/<ItemsController>
        /// <summary>
        /// Articles de venda (preus + descomptes) amb filtre
        /// per client, codi, descripció i grup.
        /// </summary>
        [HttpPost("GetItemsSales")]
        public ActionResult GetItemsSales([FromForm] string cardCode = "%",[FromForm] string itemCode = "%", [FromForm] string itemName = "%",[FromForm] string itmsGrpCod = "%" )
        {
            ItemsService SItems = new ItemsService();

            // Fem servir el nou mètode GetItemSales (no canviem la ruta HTTP)
            string jsonResult = SItems.GetItemSales(cardCode, itemCode, itemName, itmsGrpCod);

            if (jsonResult != null)
                return Ok(jsonResult);
            else
                return BadRequest(jsonResult);
        }

        /// <summary>
        /// Resum d’articles comprats per un client (CardCode)
        /// filtrat per any o per rang de dates.
        ///
        /// POST api/Items/GetItemsPurchasedSummary
        ///
        /// Body (form-data / x-www-form-urlencoded):
        ///   cardCode = codi de client SAP (OCRD.CardCode)
        ///   year     = any, opcional (p.ex. 2024)
        ///   fromDate = data inicial (YYYY-MM-DD), opcional
        ///   toDate   = data final   (YYYY-MM-DD), opcional
        ///
        /// Regles:
        ///   - Si arriba "year", fem servir tot l’exercici.
        ///   - Si no hi ha "year", fem servir fromDate + toDate.
        /// </summary>
        [HttpPost("GetItemsPurchasedSummary")]
        public ActionResult GetItemsPurchasedSummary([FromForm] string cardCode,[FromForm] string year = "",[FromForm] string fromDate = "",[FromForm] string toDate = "")
        {
            if (string.IsNullOrWhiteSpace(cardCode))
            {
                return BadRequest("Falta el CardCode.");
            }

            // 1️⃣ Calculem el rang de dates
            DateTime from;
            DateTime to;

            if (!string.IsNullOrWhiteSpace(year))
            {
                // Si ens passen l’any, agafem tot l’exercici
                if (!int.TryParse(year, out int any))
                {
                    return BadRequest("Any invàlid.");
                }

                from = new DateTime(any, 1, 1);
                to = new DateTime(any, 12, 31);
            }
            else
            {
                // Si no hi ha any, exigim fromDate + toDate
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
            var svc = new ItemsPurchasedService();
            string jsonResult = svc.GetItemsPurchasedSummary(cardCode, from, to);

            // 3️⃣ Retornem el JSON, mateix patró que la resta d’ItemsController
            if (jsonResult != null)
                return Ok(jsonResult);
            else
                return BadRequest(jsonResult);
        }

        /// <summary>
        /// Històric de compres d’un article concret per un client.
        /// 
        /// POST api/Items/GetItemPurchaseHistory
        ///
        /// Body (form-data / x-www-form-urlencoded):
        ///   cardCode = codi de client SAP (OCRD.CardCode)
        ///   itemCode = codi d'article (INV1.ItemCode)
        ///   year     = any, opcional (p.ex. 2024)
        ///   fromDate = data inicial (YYYY-MM-DD), opcional
        ///   toDate   = data final   (YYYY-MM-DD), opcional
        ///
        /// Regles:
        ///   - Si arriba "year", fem servir tot l’exercici.
        ///   - Si no hi ha "year", fem servir fromDate + toDate.
        /// </summary>
        [HttpPost("GetItemPurchasedHistory")]
        public ActionResult GetItemPurchasedHistory([FromForm] string cardCode,[FromForm] string itemCode,[FromForm] string year = "",[FromForm] string fromDate = "",[FromForm] string toDate = "")
        {
            if (string.IsNullOrWhiteSpace(cardCode))
            {
                return BadRequest("Falta el CardCode.");
            }

            if (string.IsNullOrWhiteSpace(itemCode))
            {
                return BadRequest("Falta l'ItemCode.");
            }

            // 1️⃣ Calculem el rang de dates
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
            var svc = new ItemsPurchasedService();
            string jsonResult = svc.GetItemPurchasedHistory(cardCode, itemCode, from, to);

            // 3️⃣ Retornem el JSON, mateix patró que la resta d’ItemsController
            if (jsonResult != null)
                return Ok(jsonResult);
            else
                return BadRequest(jsonResult);
        }

        /// <summary>
        /// Retorna la informació de media d'un article (via SQL, sense DI-API):
        /// U_XN_HiRes, U_XN_Thumb, U_XN_FT, U_XN_LongDesc i Proveïdor preferent.
        /// 
        /// POST api/Items/GetItemMediaInfo
        /// Body (form-data / x-www-form-urlencoded):
        ///   itemCode = codi de l'article
        /// </summary>
        [HttpPost("GetItemMediaInfo")]
        public ActionResult GetItemMediaInfo([FromForm] string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                return BadRequest("Falta el camp obligatori itemCode.");
            }

            try
            {
                var svc = new SapItemMediaService();
                var info = svc.GetItemMediaInfo(itemCode.Trim());

                // Sempre retornem JSON (application/json) amb l'objecte.
                return Ok(info);
            }
            catch (Exception ex)
            {
                // Ara com a mínim veuràs el motiu real al Swagger / client.
                return StatusCode(500, "GetItemMediaInfo ERROR: " + ex.Message);
            }
        }



        /// <summary>
        /// Processa media (imatges + fitxes tècniques) per un article
        /// i, opcionalment, les seves variants.
        /// 
        /// POST api/Items/ProcessItemMedia
        /// 
        /// Body (multipart/form-data):
        ///   itemCode   = codi d'article origen (obligatori)
        ///   imageUrl   = URL d'origen de la imatge (opcional)
        ///   imageFile  = fitxer d'imatge (opcional)
        ///   fileUrl    = URL d'origen de la fitxa tècnica (opcional)
        ///   techFile   = fitxer de fitxa tècnica (opcional)
        ///   targetItems= llista de variants separades per comes
        ///   copyImage  = flag per copiar imatge a variants
        ///   copyFicha  = flag per copiar fitxa tècnica a variants
        ///   copyDesc   = flag per copiar descripció llarga a variants
        /// </summary>
        [HttpPost("ProcessItemMedia")]
        public ActionResult ProcessItemMedia([FromForm] ProcessItemMediaRequest request)
        {
            // 🧪 En aquesta fase només fem wiring al servei de negoci.
            //     - No fem validacions fortes encara.
            //     - No toquem NAS ni DI-API.
            //     - Només retornem l'estructura de resposta “stub”.
            
            if (!ModelState.IsValid)
            {
                // Aquí podries escriure a log tots els errors i valors:
                var errors = ModelState
                    .Where(kv => kv.Value?.Errors.Count > 0)
                    .Select(kv => new {
                        Field = kv.Key,
                        Errors = kv.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    });

                // Log(errors) ...
                // Però això ja és per depuració; no cal per producció si ara va bé.
            }

            if (request == null || string.IsNullOrWhiteSpace(request.ItemCode))
            {
                return BadRequest("Falta el camp obligatori itemCode.");
            }

            var svc = new ItemMediaService();
            ProcessItemMediaResponse response = svc.ProcessItemMedia(request);

            // De moment, sempre tornem 200 OK amb el cos de resposta,
            // encara que response.Success sigui false. Més endavant
            // podrem decidir si en alguns casos retornem 400/500, etc.
            return Ok(response);
        }

        /// <summary>
        /// Retorna la miniatura de la imatge de producte com a image/jpeg.
        /// 
        /// Exemple:
        ///   GET api/Items/GetItemMediaThumbnail?itemCode=VA999999
        /// </summary>
        [HttpGet("GetItemMediaThumbnail")]
        public IActionResult GetItemMediaThumbnail([FromQuery] string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                return BadRequest("Falta el camp obligatori itemCode.");
            }

            var nasService = new NasService();

            byte[] thumbBytes;
            try
            {
                thumbBytes = nasService.GetItemThumbnailBytes(itemCode);
            }
            catch (Exception ex)
            {
                // Aquí podries fer log de l'error si cal
                return StatusCode(500, $"Error en obtenir la miniatura: {ex.Message}");
            }

            if (thumbBytes == null || thumbBytes.Length == 0)
            {
                return NotFound("No s'ha trobat cap imatge per aquest article.");
            }

            // Retornem la imatge com a JPEG perquè el navegador la pugui mostrar directament
            return File(thumbBytes, "image/jpeg");
        }

    }
}
