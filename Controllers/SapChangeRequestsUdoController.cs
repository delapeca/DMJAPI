using Microsoft.AspNetCore.Mvc;
using XNDmjApi.Infrastructure.ApiKeys;
using XNDmjApi.Models.ChangeRequestsUdo;
using XNDmjApi.Services;

namespace XNDmjApi.Controllers
{
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
}
}

