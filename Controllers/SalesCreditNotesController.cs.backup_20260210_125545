using Microsoft.AspNetCore.Mvc;
using System;
using XNDmjApi.Services;

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SalesCreditNotesController : ControllerBase
    {
        [HttpPost("GetSalesCreditNotesSummary")]
        public ActionResult GetSalesCreditNotesSummary(
            [FromForm] string cardCode = "%",
            [FromForm] string year = "",
            [FromForm] string fromDate = "",
            [FromForm] string toDate = ""
        )
        {
            try
            {
                var svc = new SalesCreditNotesService();
                var jsonResult = svc.GetSalesCreditNotesSummary(cardCode, year, fromDate, toDate);

                // Mateix patró que a la resta: el servei ja retorna JSON serialitzat
                return Content(jsonResult, "application/json");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("GetSalesCreditNoteDetail")]
        public ActionResult GetSalesCreditNoteDetail(
            [FromForm] string cardCode,
            [FromForm] int docEntry
        )
        {
            try
            {
                var svc = new SalesCreditNotesService();
                var jsonResult = svc.GetSalesCreditNoteDetail(cardCode, docEntry);

                return Content(jsonResult, "application/json");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}

