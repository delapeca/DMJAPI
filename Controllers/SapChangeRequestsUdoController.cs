using Microsoft.AspNetCore.Mvc;
using XNDmjApi.Infrastructure.ApiKeys;
using XNDmjApi.Models.ChangeRequestsUdo;
using XNDmjApi.Services;

namespace XNDmjApi.Controllers
{
public sealed class ChangeRequestApplyRequest
{
    public string? RequestRef { get; set; }
    public string? CardCode { get; set; }
}
    public sealed class ChangeRequestSetLineStatusRequest
    {
        public string? CardCode { get; set; }
        public string? Code { get; set; }
        public int LineId { get; set; }
        public string? LineStatus { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public sealed class SapChangeRequestsUdoController : ControllerBase
    {
        private readonly SapChangeRequestsUdoService _svc;

        public SapChangeRequestsUdoController(SapChangeRequestsUdoService svc)
        {
            _svc = svc;
        }

        [HttpPost("create")]
        public IActionResult Create([FromBody] ChangeRequestUdoUpsertRequest req)
        {
            var profile = HttpContext.Items[ApiKeyProfileMiddleware.HttpContextItemKey] as ApiKeyProfile;
            if (profile == null)
                return Unauthorized(new { ok = false, code = "MISSING_PROFILE", message = "Falta perfil (X-Api-Key)." });

            var (ok, code, message, requestRef) = _svc.Create(profile, req);
            if (!ok)
                return BadRequest(new ChangeRequestCreateResponse { Ok = false, Code = code, Message = message, RequestRef = null });

            return Ok(new ChangeRequestCreateResponse { Ok = true, Code = code, Message = message, RequestRef = requestRef });
        }

        [HttpPost("update-lines/{requestRef}")]
        public IActionResult UpdateLines([FromRoute] string requestRef, [FromBody] ChangeRequestUdoUpsertRequest req)
        {
            var profile = HttpContext.Items[ApiKeyProfileMiddleware.HttpContextItemKey] as ApiKeyProfile;
            if (profile == null)
                return Unauthorized(new { ok = false, code = "MISSING_PROFILE", message = "Falta perfil (X-Api-Key)." });

            var (ok, code, message) = _svc.UpdateLines(profile, requestRef, req);

            if (!ok && code == "NOT_PENDING")
                return Conflict(new ChangeRequestUpdateResponse { Ok = false, Code = code, Message = message });

            if (!ok)
                return BadRequest(new ChangeRequestUpdateResponse { Ok = false, Code = code, Message = message });

            return Ok(new ChangeRequestUpdateResponse { Ok = true, Code = code, Message = message });
        }
    
        [HttpGet("list")]
        public IActionResult List(
            [FromQuery] string cardCode,
            [FromQuery] string? kind = null,
            [FromQuery] int? targetId = null,
            [FromQuery] string? status = null,
            [FromQuery] int skip = 0,
            [FromQuery] int top = 50,
            [FromQuery] string? order = "desc")
        {
            var profile = HttpContext.Items[ApiKeyProfileMiddleware.HttpContextItemKey] as ApiKeyProfile;
            if (profile == null)
                return Unauthorized(new { ok = false, code = "MISSING_PROFILE", message = "Falta perfil (X-Api-Key)." });

            bool orderDesc = !(order ?? "desc").Trim().Equals("asc", System.StringComparison.OrdinalIgnoreCase);

            var (ok, code, message, data) = _svc.List(profile, cardCode, kind, targetId, status, skip, top, orderDesc);

            if (!ok)
                return BadRequest(new ChangeRequestListResponse { Ok = false, Code = code, Message = message, CardCode = cardCode, Items = new System.Collections.Generic.List<ChangeRequestListItemDto>(), Paging = null });

            return Ok(data);
        }

        [HttpGet("get")]
        public IActionResult Get([FromQuery] string requestRef)
        {
            var profile = HttpContext.Items[ApiKeyProfileMiddleware.HttpContextItemKey] as ApiKeyProfile;
            if (profile == null)
                return Unauthorized(new { ok = false, code = "MISSING_PROFILE", message = "Falta perfil (X-Api-Key)." });

            var (ok, code, message, data) = _svc.Get(profile, requestRef);

            if (!ok && code == "NOT_FOUND")
                return NotFound(new ChangeRequestGetResponse { Ok = false, Code = code, Message = message, Item = null });

            if (!ok)
                return BadRequest(new ChangeRequestGetResponse { Ok = false, Code = code, Message = message, Item = null });

            return Ok(data);
        }
        [HttpPost("set-line-status")]
        public IActionResult SetLineStatus([FromBody] ChangeRequestSetLineStatusRequest req)
        {
            var profile = HttpContext.Items[ApiKeyProfileMiddleware.HttpContextItemKey] as ApiKeyProfile;
            if (profile == null)
                return Unauthorized(new { ok = false, code = "MISSING_PROFILE", message = "Falta perfil (ProfileApiKey / X-Api-Key)." });

            if (req == null)
                return BadRequest(new { ok = false, code = "MISSING_BODY", message = "Falta body." });

            var cardCode = (req.CardCode ?? "").Trim();
            var code = (req.Code ?? "").Trim();
            var lineStatus = (req.LineStatus ?? "").Trim();

            if (string.IsNullOrWhiteSpace(cardCode))
                return BadRequest(new { ok = false, code = "MISSING_CARDCODE", message = "Falta cardCode." });

            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { ok = false, code = "MISSING_CODE", message = "Falta code." });

            var (ok, c, m) = _svc.SetLineStatus(profile, cardCode, code, req.LineId, lineStatus);

            if (!ok && (c == "NOT_FOUND" || c == "LINE_NOT_FOUND"))
                return NotFound(new { ok = false, code = c, message = m });

            if (!ok && (c == "INVALID_STATUS" || c == "INVALID_LINEID" || c == "MISSING_CARDCODE" || c == "MISSING_CODE"))
                return BadRequest(new { ok = false, code = c, message = m });

            if (!ok)
                return BadRequest(new { ok = false, code = c, message = m });

            return Ok(new { ok = true, code = c, message = m });
        }

    
        [HttpPost("apply")]
        public IActionResult Apply([FromBody] ChangeRequestApplyRequest req)
        {
            var profile = HttpContext.Items[ApiKeyProfileMiddleware.HttpContextItemKey] as ApiKeyProfile;
            if (profile == null)
                return Unauthorized(new { ok = false, code = "MISSING_PROFILE", message = "Falta perfil (ProfileApiKey / X-Api-Key)." });

            if (req == null)
                return BadRequest(new { ok = false, code = "MISSING_BODY", message = "Falta body." });

            var requestRef = (req.RequestRef ?? "").Trim();
            var cardCode = (req.CardCode ?? "").Trim();

            if (string.IsNullOrWhiteSpace(requestRef))
                return BadRequest(new { ok = false, code = "MISSING_REQUESTREF", message = "Falta requestRef." });

            // cardCode és opcional, però si el passes, fem match al service
            var (ok, c, m) = _svc.Apply(profile, requestRef, string.IsNullOrWhiteSpace(cardCode) ? null : cardCode);

            if (!ok)
                return BadRequest(new { ok = false, code = c, message = m });

            return Ok(new { ok = true, code = c, message = m });
        }


    }
}

