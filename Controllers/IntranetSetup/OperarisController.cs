using Microsoft.AspNetCore.Mvc;
using XNDmjApi.Services;

namespace XNDmjApi.Controllers.IntranetSetup
{
    [Route("api/intranetSetup/[controller]")]
    [ApiController]
    public class OperarisController : ControllerBase
    {
        [HttpPost("GetList")]
        public IActionResult GetList([FromForm] string cardCode, [FromForm] string? onlyActive = null)
        {
            if (string.IsNullOrWhiteSpace(cardCode))
                return BadRequest(new { error = "MISSING_CARDCODE" });

            var svc = new IntranetSetupOperarisService();
            var json = svc.GetList(cardCode.Trim(), (onlyActive ?? "").Trim());
            return Content(json, "application/json");
        }

        [HttpPost("GetDetail")]
        public IActionResult GetDetail([FromForm] string cardCode, [FromForm] int code)
        {
            if (string.IsNullOrWhiteSpace(cardCode))
                return BadRequest(new { error = "MISSING_CARDCODE" });

            var svc = new IntranetSetupOperarisService();
            var json = svc.GetDetail(cardCode.Trim(), code);
            return Content(json, "application/json");
        }

        [HttpPost("Create")]
        public IActionResult Create(
            [FromForm] string cardCode,
            [FromForm] string? name = null,
            [FromForm] string? dni = null,
            [FromForm] string? telefon = null,
            [FromForm] string? observacions = null,
            [FromForm] string? encarregat = null
        )
        {
            if (string.IsNullOrWhiteSpace(cardCode))
                return BadRequest(new { error = "MISSING_CARDCODE" });

            var enc = (encarregat ?? "N").Trim().ToUpperInvariant();
            if (enc != "Y" && enc != "N")
                return BadRequest(new { error = "INVALID_ENCARREGAT", expected = "Y|N" });

            var svc = new IntranetSetupOperarisService();
            var json = svc.Create(
                cardCode.Trim(),
                (name ?? "").Trim(),
                (dni ?? "").Trim(),
                (telefon ?? "").Trim(),
                (observacions ?? "").Trim(),
                enc
            );
            return Content(json, "application/json");
        }

        [HttpPost("Update")]
        public IActionResult Update(
            [FromForm] string cardCode,
            [FromForm] int code,
            [FromForm] string? name = null,
            [FromForm] string? dni = null,
            [FromForm] string? telefon = null,
            [FromForm] string? observacions = null,
            [FromForm] string? encarregat = null
        )
        {
            if (string.IsNullOrWhiteSpace(cardCode))
                return BadRequest(new { error = "MISSING_CARDCODE" });

            var enc = (encarregat ?? "N").Trim().ToUpperInvariant();
            if (enc != "Y" && enc != "N")
                return BadRequest(new { error = "INVALID_ENCARREGAT", expected = "Y|N" });

            var svc = new IntranetSetupOperarisService();
            var json = svc.Update(
                cardCode.Trim(),
                code,
                (name ?? "").Trim(),
                (dni ?? "").Trim(),
                (telefon ?? "").Trim(),
                (observacions ?? "").Trim(),
                enc
            );
            return Content(json, "application/json");
        }

        [HttpPost("SetActive")]
        public IActionResult SetActive([FromForm] string cardCode, [FromForm] int code, [FromForm] string actiu)
        {
            if (string.IsNullOrWhiteSpace(cardCode))
                return BadRequest(new { error = "MISSING_CARDCODE" });

            var a = (actiu ?? "").Trim().ToUpperInvariant();
            if (a != "Y" && a != "N")
                return BadRequest(new { error = "INVALID_ACTIU", expected = "Y|N" });

            var svc = new IntranetSetupOperarisService();
            var json = svc.SetActive(cardCode.Trim(), code, a);
            return Content(json, "application/json");
        }
    }
}