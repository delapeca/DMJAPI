using Microsoft.AspNetCore.Mvc;
using XNDmjApi.Functions;
using XNDmjApi.Models.DbSetup;
using XNDmjApi.Services;

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class DbSetupController : ControllerBase
    {
        private readonly SAPLoginService _sapLogin = new SAPLoginService();

        // POST api/DbSetup/EnsureSchema
        // Només Intranet: requereix userToken SAP i grup Admin/Advanced
        [HttpPost("EnsureSchema")]
        [Consumes("application/json")]
        public ActionResult EnsureSchema(
            [FromBody] EnsureSchemaRequest body,
            [FromHeader(Name = "X-DMJ-Confirm-Db")] string? confirmDb
        )
        {
            var userToken = body?.UserToken ?? "";
            var schema = body?.Schema;

            if (string.IsNullOrWhiteSpace(userToken))
                return BadRequest(new { ok = false, error = "MISSING_USERTOKEN" });

            if (!_sapLogin.ValidateUserToken(userToken))
                return BadRequest(new { ok = false, error = "TOKEN_EXPIRED" });

            // IMPORTANT: La DB ve del context SAP (Dades.DOMENJO_BBDD). NO fallback.
            Response.Headers["X-DMJ-DB"] = (Dades.DOMENJO_BBDD ?? "").ToString();
            if (string.IsNullOrWhiteSpace(Dades.DOMENJO_BBDD))
                return BadRequest(new { ok = false, error = "DB_CONTEXT_MISSING" });

            // Guard extra: confirmació explícita de DB
            if (string.IsNullOrWhiteSpace(confirmDb) ||
                !string.Equals(confirmDb.Trim(), Dades.DOMENJO_BBDD.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    ok = false,
                    error = "DB_CONFIRM_REQUIRED",
                    message = "Header X-DMJ-Confirm-Db no coincideix amb la DB del context.",
                    expected = Dades.DOMENJO_BBDD
                });
            }

            if (!_sapLogin.IsAdminOrAdvancedFromToken(userToken))
                return StatusCode(403, new { ok = false, error = "NOT_AUTHORIZED" });

            if (schema == null || schema.Tables == null || schema.Tables.Count == 0)
                return BadRequest(new { ok = false, error = "MISSING_SCHEMA" });

            try
            {
                var svc = new SqlSchemaService();
                var resp = svc.Ensure(schema); // DI-API UDT/UDO
                return Ok(resp);
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { ok = false, error = "EXCEPTION", message = ex.Message });
            }
        }
    }
}
