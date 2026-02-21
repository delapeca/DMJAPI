using Microsoft.AspNetCore.Mvc;
using System;
using System.Globalization;
using XNDmjApi.Services;
using XNDmjApi.Infrastructure.ApiKeys;
using XNDmjApi.Functions;  // ?? AFEGIT per ProfileApiKey

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SalesInvoicesController : ControllerBase
    {
        /// <summary>
        /// Resum de factures de venda per un client (CardCode).
        ///
        /// Accepta:
        ///   - year = "2024" (opcional) ? si està informat, es fa servir l'any complet
        ///   - fromDate / toDate (yyyy-MM-dd) ? si no hi ha year, s'ha d'informar rang de dates
        ///   - status ? filtre de DocStatus (p.ex. "O", "C", "%"…)
        ///   - docNum / numAtCard ? filtres opcionals per número de document i referència client
        ///
        /// Endpoint:
        ///   POST api/SalesInvoices/GetSalesInvoicesSummary
        /// </summary>
        [HttpPost("GetSalesInvoicesSummary")]
        public ActionResult GetSalesInvoicesSummary(
            [FromForm] string cardCode = "%",
            [FromForm] string year = "",
            [FromForm] string fromDate = "",
            [FromForm] string toDate = "",
            [FromForm] string status = "%",      // de moment NO l'usem a la query
            [FromForm] string docNum = "",
            [FromForm] string numAtCard = ""
            )
        {
            // ?? AFEGIT: Validació de perfil i selecció de BD
            var fail = RequireProfileAndSelectDb(out var profile);
            if (fail != null) return fail;

            try
            {
                // 1?? Normalitzar CardCode
                if (string.IsNullOrWhiteSpace(cardCode))
                    cardCode = "%";

                // 2?? Resolució de dates: any complet o from/to
                DateTime from;
                DateTime to;

                if (!string.IsNullOrWhiteSpace(year))
                {
                    if (!int.TryParse(year, out int y))
                    {
                        return BadRequest("El paràmetre 'year' no és un enter vàlid.");
                    }

                    from = new DateTime(y, 1, 1);
                    to = new DateTime(y, 12, 31);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate))
                        return BadRequest("fromDate i toDate són obligatoris si no s'indica year.");

                    from = DateTime.ParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    to = DateTime.ParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                // 3?? Cridar el servei (sense status, només 5 paràmetres)
                var svc = new SalesInvoicesService();

                string jsonResult = svc.GetSalesInvoicesSummary(
                    cardCode,
                    from,
                    to,
                    docNum ?? string.Empty,
                    numAtCard ?? string.Empty
                );

                if (jsonResult == null)
                    return BadRequest("Error generant el JSON de resum de factures.");

                return Content(jsonResult, "application/json");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }


        /// <summary>
        /// Detall d'una factura de venda concreta (capçalera + línies).
        ///
        /// Endpoint:
        ///   POST api/SalesInvoices/GetSalesInvoiceDetail
        ///
        /// Body (x-www-form-urlencoded / form-data):
        ///   - cardCode  ? Codi client (per coherència amb la resta de mòduls)
        ///   - docEntry  ? DocEntry de la factura (OINV.DocEntry)
        /// </summary>
        [HttpPost("GetSalesInvoiceDetail")]
        public ActionResult GetSalesInvoiceDetail(
            [FromForm] string cardCode,
            [FromForm] int docEntry
        )
        {
            // ?? AFEGIT: Validació de perfil i selecció de BD
            var fail = RequireProfileAndSelectDb(out var profile);
            if (fail != null) return fail;

            if (string.IsNullOrWhiteSpace(cardCode))
                return BadRequest("Falta cardCode.");

            if (docEntry <= 0)
                return BadRequest("docEntry invàlid.");

            var svc = new SalesInvoicesService();
            string jsonResult = svc.GetSalesInvoiceDetail(cardCode, docEntry);

            // Seguint el patró de SalesOrdersController / SalesReturnsController
            return Ok(jsonResult);
        }


        // =====================================================================
        // Helper central: Perfil + Selecció de BD (mateix patró que ItemsController)
        // ---------------------------------------------------------------------
        // Ús a cada endpoint:
        // var fail = RequireProfileAndSelectDb(out var profile);
        // if (fail != null) return fail;
        //
        // Depèn de:
        // - ApiKeyProfileMiddleware (middleware) resol el perfil i el posa a:
        //   HttpContext.Items[ApiKeyProfileMiddleware.HttpContextItemKey]
        // - ApiKeyProfile.CompanyDb és el selector de DB (PROD/TEST)
        //
        // IMPORTANT: Dades.* és estàtic/global ? risc en concurrència amb perfils diferents.
        // NO es toca ara: només repliquem el patró existent.
        // =====================================================================
        private ActionResult? RequireProfileAndSelectDb(out ApiKeyProfile? profile)
        {
            // 1) Recupera perfil resolt pel middleware
            profile = HttpContext.Items[ApiKeyProfileMiddleware.HttpContextItemKey] as ApiKeyProfile;
            if (profile == null)
            {
                return Unauthorized(new
                {
                    ok = false,
                    code = "MISSING_PROFILE",
                    message = "Falta perfil (ProfileApiKey o X-Api-Key)."
                });
            }

            // 2) CompanyDb és el "selector" de la BD
            var db = (profile.CompanyDb ?? "").Trim();
            if (string.IsNullOrWhiteSpace(db))
            {
                return StatusCode(500, new
                {
                    ok = false,
                    code = "DB_CONTEXT_MISSING",
                    message = "El perfil no porta CompanyDb."
                });
            }

            // 3) Inicialitza / canvia DB context abans de cridar serveis SQL
            try
            {
                // Recalcula només si cal:
                // - si canvia el DB
                // - o si encara no tenim ConnectionStringDOMENJO
                if (!string.Equals(Dades.DOMENJO_BBDD ?? "", db, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(Dades.ConnectionStringDOMENJO))
                {
                    Dades.DOMENJO_BBDD = db;
                    Dades.SetupDades();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    ok = false,
                    code = "DB_SELECT_FAILED",
                    message = ex.Message
                });
            }

            return null; // OK
        }
    }
}

