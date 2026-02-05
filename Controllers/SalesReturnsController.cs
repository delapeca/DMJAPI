using Microsoft.AspNetCore.Mvc;
using System;
using System.Globalization;
using XNDmjApi.Services;

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SalesReturnsController : ControllerBase
    {
        [HttpPost("GetSalesReturnsSummary")]
        public ActionResult GetSalesReturnsSummary(
            [FromForm] string cardCode = "%",
            [FromForm] string year = "",
            [FromForm] string fromDate = "",
            [FromForm] string toDate = ""
        )
        {
            try
            {
                DateTime from;
                DateTime to;

                if (!string.IsNullOrWhiteSpace(year))
                {
                    int y = int.Parse(year);
 




























                   from = new DateTime(y, 1, 1);
                    to   = new DateTime(y, 12, 31);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate))
                        return BadRequest("fromDate i toDate són obligatoris si no s'indica year.");

                    from = DateTime.ParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    to   = DateTime.ParseExact(toDate,   "yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                var svc = new SalesReturnsService();
                var jsonResult = svc.GetSalesReturnsSummary(cardCode, from, to);

                return Content(jsonResult, "application/json");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
 



















           }
        }

        [HttpPost("GetSalesReturnDetail")]
        public ActionResult GetSalesReturnDetail(
            [FromForm] string cardCode = "%",
            [FromForm] int docEntry = 0
        )
        {
            try
            {
                var svc = new SalesReturnsService();
                var jsonResult = svc.GetSalesReturnDetail(cardCode, docEntry);

                return Ok(jsonResult);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
