using Microsoft.AspNetCore.Mvc;
using XNDmjApi.Services;

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChangeRequestsController : ControllerBase
    {
        private readonly SAPLoginService _sapLogin = new SAPLoginService();

        // POST api/ChangeRequests/CreateContactChangeRequest
        // ⚠️ Només per Intranet: requereix userToken SAP i grup Admin/Advanced
        [HttpPost("CreateContactChangeRequest")]
        public IActionResult CreateContactChangeRequest(
            [FromForm] string userToken,
            [FromForm] string cardCode,
            [FromForm] string action,
            [FromForm] int? targetId = null,
            [FromForm] string? name = null,
            [FromForm] string? address = null,
            [FromForm] string? tel1 = null,
            [FromForm] string? tel2 = null,
            [FromForm] string? cellolar = null,
            [FromForm] string? e_MailL = null,
            [FromForm] string? firstName = null,
            [FromForm] string? middleName = null,
            [FromForm] string? lastName = null,
            [FromForm] bool? receiveSalesDocs = null,
            [FromForm] string? reason = null,
            [FromForm] int? requestedByUserId = null,
            [FromForm] string? requestedByEmail = null
        )
        {
            if (!_sapLogin.ValidateUserToken(userToken))
                return BadRequest(new { ok = false, error = "TOKEN_EXPIRED" });

            if (!_sapLogin.IsAdminOrAdvancedFromToken(userToken))
                return StatusCode(403, new { ok = false, error = "NOT_AUTHORIZED" });

            var svc = new SapChangeRequestsService();

            var input = new SapChangeRequestsService.CreateContactChangeRequestInput
            {
                CardCode = cardCode,
                Action = action,
                TargetId = targetId,
                Name = name,
                Address = address,
                Tel1 = tel1,
                Tel2 = tel2,
                Cellular = cellolar,
                E_MailL = e_MailL,
                FirstName = firstName,
                MiddleName = middleName,
                LastName = lastName,
                ReceiveSalesDocs = receiveSalesDocs,
                Reason = reason,
                RequestedByUserId = requestedByUserId,
                RequestedByEmail = requestedByEmail
            };

            var (ok, code, message, requestRef, docEntry, docNum) = svc.CreateContactChangeRequest(input);

            if (!ok)
                return BadRequest(new { ok = false, error = code, message });

            return Ok(new { ok = true, code, message, requestRef, docEntry, docNum });
        }
    }
}
