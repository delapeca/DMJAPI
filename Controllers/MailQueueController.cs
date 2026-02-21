using Microsoft.AspNetCore.Mvc;
using XNDmjApi.Functions;
using XNDmjApi.Models.MailQueue;
using XNDmjApi.Services;

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class MailQueueController : ControllerBase
    {
        private readonly SAPLoginService _sapLogin = new SAPLoginService();

        // POST api/MailQueue/Enqueue
        [HttpPost("Enqueue")]
        [Consumes("application/json")]
        public ActionResult Enqueue(
            [FromBody] MailQueueEnqueueRequest body,
            [FromHeader(Name = "userToken")] string? userToken
        )
        {
            userToken = (userToken ?? "").Trim();
            if (string.IsNullOrWhiteSpace(userToken))
                return BadRequest(new { ok = false, error = "MISSING_USERTOKEN_HEADER" });

            if (!_sapLogin.ValidateUserToken(userToken))
                return BadRequest(new { ok = false, error = "TOKEN_EXPIRED" });

            Response.Headers["X-DMJ-DB"] = (Dades.DOMENJO_BBDD ?? "").ToString();
            if (string.IsNullOrWhiteSpace(Dades.DOMENJO_BBDD))
                return BadRequest(new { ok = false, error = "DB_CONTEXT_MISSING" });

            if (Dades.oCompany == null || !Dades.oCompany.Connected)
                return BadRequest(new { ok = false, error = "SAP_CONTEXT_MISSING" });

            if (string.IsNullOrWhiteSpace(body.ObjType))
                return BadRequest(new { ok = false, error = "MISSING_OBJTYPE" });

            if (body.DocEntry <= 0)
                return BadRequest(new { ok = false, error = "MISSING_DOCENTRY" });

            try
            {
                var svc = new SapMailQueueService();
                var resp = svc.Enqueue(userToken, body);   // <-- canvi aquí
                return Ok(resp);
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { ok = false, error = "EXCEPTION", message = ex.Message });
            }
        }

    }
}

