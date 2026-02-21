using Microsoft.AspNetCore.Mvc;
using XNDmjApi.Services;

namespace XNDmjApi.Controllers.IntranetSetup
{
    [Route("api/intranetSetup/[controller]")]
    [ApiController]
    public class ObresController : ControllerBase
    {
        [HttpPost("GetList")]
        public IActionResult GetList([FromForm] string cardCode, [FromForm] string? onlyActive = null)
        {
            if (string.IsNullOrWhiteSpace(cardCode))
                return BadRequest(new { error = "MISSING_CARDCODE" });

            var svc = new IntranetSetupObresService();
            var json = svc.GetList(cardCode.Trim(), (onlyActive ?? "").Trim());
            return Content(json, "application/json");
        }

        [HttpPost("GetDetail")]
        public IActionResult GetDetail([FromForm] string cardCode, [FromForm] int code)
        {
            if (string.IsNullOrWhiteSpace(cardCode))
                return BadRequest(new { error = "MISSING_CARDCODE" });

            var svc = new IntranetSetupObresService();
            var json = svc.GetDetail(cardCode.Trim(), code);
            return Content(json, "application/json");
        }

        [HttpPost("Create")]
        public IActionResult Create(
            [FromForm] string cardCode,
            [FromForm] string name,
            [FromForm] string? alias = null,
            [FromForm] string? adreca = null,
            [FromForm] string? ubicacio = null,
            [FromForm] string? contacte = null,
            [FromForm] string? telefon = null,
            [FromForm] string? observacions = null
        )
        {
            if (string.IsNullOrWhiteSpace(cardCode))
                return BadRequest(new { error = "MISSING_CARDCODE" });

            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { error = "MISSING_NAME" });

            var svc = new IntranetSetupObresService();
            var json = svc.Create(
                cardCode.Trim(),
                name.Trim(),
                (alias ?? "").Trim(),
                (adreca ?? "").Trim(),
                (ubicacio ?? "").Trim(),
                (contacte ?? "").Trim(),
                (telefon ?? "").Trim(),
                (observacions ?? "").Trim()
            );
            return Content(json, "application/json");
        }

        [HttpPost("UpdateOpenFields")]
        public IActionResult UpdateOpenFields(
            [FromForm] string cardCode,
            [FromForm] int code,
            [FromForm] string? contacte = null,
            [FromForm] string? telefon = null,
            [FromForm] string? observacions = null
        )
        {
            if (string.IsNullOrWhiteSpace(cardCode))
                return BadRequest(new { error = "MISSING_CARDCODE" });

            var svc = new IntranetSetupObresService();
            var json = svc.UpdateOpenFields(
                cardCode.Trim(),
                code,
                (contacte ?? "").Trim(),
                (telefon ?? "").Trim(),
                (observacions ?? "").Trim()
            );
            return Content(json, "application/json");
        }

        [HttpPost("SetActive")]
        public IActionResult SetActive([FromForm] string cardCode, [FromForm] int code, [FromForm] string activa)
        {
            if (string.IsNullOrWhiteSpace(cardCode))
                return BadRequest(new { error = "MISSING_CARDCODE" });

            var a = (activa ?? "").Trim().ToUpperInvariant();
            if (a != "Y" && a != "N")
                return BadRequest(new { error = "INVALID_ACTIVA", expected = "Y|N" });

            var svc = new IntranetSetupObresService();
            var json = svc.SetActive(cardCode.Trim(), code, a);
            return Content(json, "application/json");
        }
    }
}