using Microsoft.AspNetCore.Mvc;
using XNDmjApi.Services;

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SapSetupController : ControllerBase
    {
        private readonly SAPLoginService _sapLogin = new SAPLoginService();

        // POST api/SapSetup/CreateChangeRequestsUDO
        // ⚠️ Només per Intranet: requereix userToken SAP i grup Admin/Advanced
        [HttpPost("CreateChangeRequestsUDO")]
        public IActionResult CreateChangeRequestsUDO([FromForm] string userToken)
        {
            if (!_sapLogin.ValidateUserToken(userToken))
                return BadRequest(new { ok = false, error = "TOKEN_EXPIRED" });

            if (!_sapLogin.IsAdminOrAdvancedFromToken(userToken))
                return StatusCode(403, new { ok = false, error = "NOT_AUTHORIZED" });

            var svc = new SapChangeRequestsUdoSetupService();
            var (ok, code, message) = svc.EnsureCreated();

            if (!ok)
                return BadRequest(new { ok = false, error = code, message });

            return Ok(new { ok = true, code, message });
        }
    }
}
