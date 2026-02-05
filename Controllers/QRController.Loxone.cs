// Controllers/QRController.Loxone.cs
//
// DMJAPI · Port de XNLoxoneAPI dins QRController (partial)
//
// Endpoints:
//  - GET  /q/{token}                      -> executa token (ús públic)
//  - POST /api/loxone/execute             -> executa comanda directa (opcional, ús intern)
//  - ADMIN (requereix X-Api-Key):
//      ACTIONS:
//        GET/POST/PUT/DELETE /api/loxone/actions
//      LOCATIONS:
//        GET/POST/PUT/DELETE /api/loxone/locations
//      LOCATION-ACTION (1 acció per ubicació):
//        GET/PUT/DELETE     /api/loxone/location-action/{locationCode}
//      LOCATION-ACTIONS (llistat global):
//        GET               /api/loxone/location-actions
//
// Notes:
//  - Admin bloquejat si Admin:ApiKey no està configurada (seguretat per defecte).
//  - EF Core Sqlite: EnsureCreated() es crida abans d'operar amb taules (evita errors en entorns nous).
//  - IMPORTANT (sense migracions):
//      - EnsureCreated() NO afegeix columnes noves a una DB existent.
//      - EnsureLoxoneSchemaAsync() aplica ALTER TABLE ADD COLUMN quan falten columnes.
//  - Rutes absolutes "~/" per mantenir compatibilitat amb URLs existents (p.ex. /q/{token}).
//
// Proteccions d'esborrat:
//  - NO es pot esborrar una Action si està referenciada a LocationActions (409 ACTION_IN_USE).
//  - NO es pot esborrar una Location si té una vinculació a LocationActions (409 LOCATION_IN_USE).
//  - Per eliminar, primer cal DELETE /api/loxone/location-action/{locationCode} (desvincular), i després DELETE action/location.

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using XNDmjApi.Loxone.Data;
using XNDmjApi.Loxone.Models;
using XNDmjApi.Loxone.Options;
using XNDmjApi.Loxone.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;


namespace XNDmjApi.Controllers
{
    public partial class QRController : ControllerBase
    {
        private readonly AppDbContext _loxDb;
        private readonly TokenService _tokens;
        private readonly LoxoneCommandService _loxCmd;
        private readonly AdminOptions _admin;
        private readonly ILogger<QRController> _logger;


        public QRController(
            IWebHostEnvironment env,
            AppDbContext loxDb,
            TokenService tokens,
            LoxoneCommandService loxCmd,
            ILogger<QRController> logger,
            IOptions<AdminOptions> adminOpt
        ) : this(env)
        {
            _loxDb = loxDb;
            _tokens = tokens;
            _loxCmd = loxCmd;
            _logger = logger;
            _admin = adminOpt.Value ?? new AdminOptions();
        }


        // ==========================================================
        // Helpers
        // ==========================================================

        /// <summary>
        /// Inicialitza DB loxone (SQLite) i aplica compatibilitat d'esquema (sense migracions).
        /// </summary>
        private async Task EnsureLoxoneDbAsync(CancellationToken ct)
        {
            // Sense migracions encara: EnsureCreated és suficient per MVP i entorns nous.
            await _loxDb.Database.EnsureCreatedAsync(ct);

            // IMPORTANT: si la DB ja existia, EnsureCreated() NO afegeix columnes noves.
            await EnsureLoxoneSchemaAsync(ct);
        }

        /// <summary>
        /// Aplica ALTER TABLE per afegir columnes noves a una base existent, si falten.
        /// Actualment: Locations(IsActive, SortOrder).
        /// </summary>
        private async Task EnsureLoxoneSchemaAsync(CancellationToken ct)
        {
            var conn = _loxDb.Database.GetDbConnection();
            await conn.OpenAsync(ct);

            try
            {
                var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // PRAGMA table_info retorna: cid, name, type, notnull, dflt_value, pk
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info('Locations');";
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        var name = r.GetString(1);
                        cols.Add(name);
                    }
                }

                // Afegir IsActive si no hi és
                if (!cols.Contains("IsActive"))
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "ALTER TABLE Locations ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1;";
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // Afegir SortOrder si no hi és
                if (!cols.Contains("SortOrder"))
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "ALTER TABLE Locations ADD COLUMN SortOrder INTEGER NULL;";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        /// <summary>
        /// Autorització admin via header X-Api-Key.
        /// Seguretat per defecte: si no hi ha Admin:ApiKey configurada, endpoints admin desactivats.
        /// </summary>
        private bool IsAdminAuthorized(out IActionResult? failure)
        {
            failure = null;

            var configured = (_admin.ApiKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(configured))
            {
                failure = StatusCode(503, new
                {
                    error = "ADMIN_DISABLED",
                    detail = "Configura Admin:ApiKey per habilitar endpoints admin."
                });
                return false;
            }

            var header = Request.Headers["X-Api-Key"].FirstOrDefault()
                    ?? Request.Headers["X-API-Key"].FirstOrDefault()
                    ?? Request.Headers["x-api-key"].FirstOrDefault();


            if (string.IsNullOrWhiteSpace(header) ||
                !string.Equals(header.Trim(), configured, StringComparison.Ordinal))
            {
                failure = Unauthorized(new { error = "UNAUTHORIZED" });
                return false;
            }

            return true;
        }

        // ==========================================================
        // PUBLIC: GET /q/{token}
        // ==========================================================
        //
        // Compatible amb expo.domenjo.cat/q/{token} (si apunta a DMJAPI).
        // Executa l'acció associada al token i marca ús.
        //
        [HttpGet("~/q/{token}")]
        public async Task<IActionResult> Q([FromRoute] string token, CancellationToken ct)
        {
            await EnsureLoxoneDbAsync(ct);

            var (t, err) = await _tokens.ValidateForUseAsync(token, ct);
            if (t is null)
                return BadRequest(new { error = err ?? "INVALID_TOKEN" });

            // Busca l'acció per code
            var action = await _loxDb.LoxoneActions
                .AsNoTracking()
                .SingleOrDefaultAsync(a => a.Code == t.ActionCode, ct);

            if (action is null)
                return NotFound(new { error = "ACTION_NOT_FOUND", actionCode = t.ActionCode });

            // Executa comanda
            var (ok, body, urlPath) = await _loxCmd.SendLightCommandAsync(action.LoxoneCommand, ct);

            // Marca token usat
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _tokens.MarkUsedAsync(t, ip, ct);

            return Ok(new
            {
                ok,
                token = t.Token,
                actionCode = action.Code,
                actionName = action.Name,
                loxonePath = urlPath,
                response = body
            });
        }

        // ==========================================================
        // OPTIONAL: POST /api/loxone/execute  (ús intern)
        // ==========================================================

        public class ExecuteRequest
        {
            /// <summary>
            /// "dev/..." o "SET(...)" segons LoxoneCommandService
            /// </summary>
            public string? Command { get; set; }
        }

        [HttpPost("~/api/loxone/execute")]
        public async Task<IActionResult> Execute([FromBody] ExecuteRequest req, CancellationToken ct)
        {
            // Si vols que això sigui només admin, descomenta:
            // if (!IsAdminAuthorized(out var fail)) return fail!;

            if (req is null || string.IsNullOrWhiteSpace(req.Command))
                return BadRequest(new { error = "MISSING_COMMAND" });

            var (ok, body, urlPath) = await _loxCmd.SendLightCommandAsync(req.Command.Trim(), ct);
            return Ok(new { ok, loxonePath = urlPath, response = body });
        }

        // ==========================================================
        // ADMIN: ACTIONS CRUD  (/api/loxone/actions)
        // ==========================================================

        public class ActionUpsert
        {
            public string? Code { get; set; }
            public string? Name { get; set; }
            public string? LoxoneCommand { get; set; }

            // Nota: no implementem IsActive/SortOrder a Actions (encara).
        }

        [HttpGet("~/api/loxone/actions")]
        public async Task<IActionResult> Admin_ListActions(CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            var items = await _loxDb.LoxoneActions
                .AsNoTracking()
                .OrderBy(x => x.Code)
                .ToListAsync(ct);

            return Ok(items);
        }

        [HttpPost("~/api/loxone/actions")]
        public async Task<IActionResult> Admin_CreateAction([FromBody] ActionUpsert req, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            if (req is null) return BadRequest(new { error = "INVALID_BODY" });

            var code = (req.Code ?? "").Trim();
            var name = (req.Name ?? "").Trim();
            var cmd = (req.LoxoneCommand ?? "").Trim();

            if (string.IsNullOrWhiteSpace(code) ||
                string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(cmd))
            {
                return BadRequest(new
                {
                    error = "MISSING_FIELDS",
                    required = new[] { "Code", "Name", "LoxoneCommand" }
                });
            }

            var exists = await _loxDb.LoxoneActions.AnyAsync(x => x.Code == code, ct);
            if (exists) return Conflict(new { error = "CODE_EXISTS", code });

            var entity = new LoxoneAction
            {
                Code = code,
                Name = name,
                LoxoneCommand = cmd
            };

            _loxDb.LoxoneActions.Add(entity);
            await _loxDb.SaveChangesAsync(ct);

            return Ok(entity);
        }

        [HttpPut("~/api/loxone/actions/{id:int}")]
        public async Task<IActionResult> Admin_UpdateAction([FromRoute] int id, [FromBody] ActionUpsert req, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            var entity = await _loxDb.LoxoneActions.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return NotFound(new { error = "NOT_FOUND" });

            var code = (req?.Code ?? entity.Code).Trim();
            var name = (req?.Name ?? entity.Name).Trim();
            var cmd = (req?.LoxoneCommand ?? entity.LoxoneCommand).Trim();

            if (string.IsNullOrWhiteSpace(code) ||
                string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(cmd))
            {
                return BadRequest(new { error = "MISSING_FIELDS" });
            }

            if (!string.Equals(entity.Code, code, StringComparison.Ordinal))
            {
                var exists = await _loxDb.LoxoneActions.AnyAsync(x => x.Code == code && x.Id != id, ct);
                if (exists) return Conflict(new { error = "CODE_EXISTS", code });
            }

            entity.Code = code;
            entity.Name = name;
            entity.LoxoneCommand = cmd;

            await _loxDb.SaveChangesAsync(ct);
            return Ok(entity);
        }

        [HttpDelete("~/api/loxone/actions/{id:int}")]
        public async Task<IActionResult> Admin_DeleteAction([FromRoute] int id, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            var entity = await _loxDb.LoxoneActions.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return NotFound(new { error = "NOT_FOUND" });

            // Protecció: no eliminar si està vinculada a LocationActions
            var inUse = await _loxDb.LocationActions.AnyAsync(x => x.LoxoneActionId == id, ct);
            if (inUse)
            {
                return Conflict(new
                {
                    error = "ACTION_IN_USE",
                    detail = "No es pot eliminar l'acció perquè està vinculada a una ubicació. Desvincula-la primer (DELETE location-action/{locationCode}).",
                    actionId = id,
                    actionCode = entity.Code
                });
            }

            _loxDb.LoxoneActions.Remove(entity);
            await _loxDb.SaveChangesAsync(ct);

            return Ok(new { ok = true });
        }

        // ==========================================================
        // ADMIN: LOCATIONS CRUD  (/api/loxone/locations)
        // ==========================================================

        public class LocationUpsert
        {
            public string? Code { get; set; }
            public string? Name { get; set; }

            // Camps de manteniment (port XNLoxoneAPI)
            // null = no canviar (en update)
            public bool? IsActive { get; set; }
            public int? SortOrder { get; set; }
        }

        [HttpGet("~/api/loxone/locations")]
        public async Task<IActionResult> Admin_ListLocations(CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            var items = await _loxDb.Locations
                .AsNoTracking()
                .OrderBy(x => x.SortOrder == null)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Code)
                .ToListAsync(ct);

            return Ok(items);
        }

        [HttpPost("~/api/loxone/locations")]
        public async Task<IActionResult> Admin_CreateLocation([FromBody] LocationUpsert req, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            var code = (req?.Code ?? "").Trim();
            var name = (req?.Name ?? "").Trim();

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new
                {
                    error = "MISSING_FIELDS",
                    required = new[] { "Code", "Name" }
                });
            }

            var exists = await _loxDb.Locations.AnyAsync(x => x.Code == code, ct);
            if (exists) return Conflict(new { error = "CODE_EXISTS", code });

            var entity = new Location
            {
                Code = code,
                Name = name,
                IsActive = req?.IsActive ?? true,
                SortOrder = req?.SortOrder
            };

            _loxDb.Locations.Add(entity);
            await _loxDb.SaveChangesAsync(ct);

            return Ok(entity);
        }

        [HttpPut("~/api/loxone/locations/{id:int}")]
        public async Task<IActionResult> Admin_UpdateLocation([FromRoute] int id, [FromBody] LocationUpsert req, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            var entity = await _loxDb.Locations.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return NotFound(new { error = "NOT_FOUND" });

            var code = (req?.Code ?? entity.Code).Trim();
            var name = (req?.Name ?? entity.Name).Trim();

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                return BadRequest(new { error = "MISSING_FIELDS" });

            if (!string.Equals(entity.Code, code, StringComparison.Ordinal))
            {
                var exists = await _loxDb.Locations.AnyAsync(x => x.Code == code && x.Id != id, ct);
                if (exists) return Conflict(new { error = "CODE_EXISTS", code });
            }

            entity.Code = code;
            entity.Name = name;

            // Flags de manteniment
            if (req?.IsActive is not null) entity.IsActive = req.IsActive.Value;
            entity.SortOrder = req?.SortOrder;

            await _loxDb.SaveChangesAsync(ct);
            return Ok(entity);
        }

        [HttpDelete("~/api/loxone/locations/{id:int}")]
        public async Task<IActionResult> Admin_DeleteLocation([FromRoute] int id, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            var entity = await _loxDb.Locations.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return NotFound(new { error = "NOT_FOUND" });

            // Protecció: no eliminar si té vinculació (1:1 per LocationId)
            var inUse = await _loxDb.LocationActions.AnyAsync(x => x.LocationId == id, ct);
            if (inUse)
            {
                return Conflict(new
                {
                    error = "LOCATION_IN_USE",
                    detail = "No es pot eliminar la ubicació perquè té una acció vinculada. Desvincula-la primer (DELETE location-action/{locationCode}).",
                    locationId = id,
                    locationCode = entity.Code
                });
            }

            _loxDb.Locations.Remove(entity);
            await _loxDb.SaveChangesAsync(ct);

            return Ok(new { ok = true });
        }

        // ==========================================================
        // ADMIN: LOCATION-ACTION mapping (1 acció per ubicació)
        //   GET    /api/loxone/location-action/{locationCode}
        //   PUT    /api/loxone/location-action/{locationCode}  { actionCode }
        //   DELETE /api/loxone/location-action/{locationCode}  -> desvincula
        // ==========================================================

        public class LocationActionSet
        {
            public string? ActionCode { get; set; }

            // IMPORTANT: Swagger ho mostra i DB ho té (LocationActions.IsDefault)
            // Si no ve (null), no canviem res en update.
            public bool? IsDefault { get; set; }
        }

        // ==========================================================
        // ADMIN: LOCATION-ACTIONS (llistat global)
        //   GET /api/loxone/location-actions
        // ==========================================================
        [HttpGet("~/api/loxone/location-actions")]
        public async Task<IActionResult> Admin_ListLocationActions(CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            // Inner join: només retornem ubicacions que tenen vinculació
            var rows = await _loxDb.LocationActions
                .AsNoTracking()
                .Include(x => x.Location)
                .Include(x => x.LoxoneAction)
                .OrderBy(x => x.Location.Code)
                .Select(x => new
                {
                    linkId = x.Id,
                    isDefault = x.IsDefault,
                    location = new { x.Location.Id, x.Location.Code, x.Location.Name },
                    action = new { x.LoxoneAction.Id, x.LoxoneAction.Code, x.LoxoneAction.Name, x.LoxoneAction.LoxoneCommand }
                })
                .ToListAsync(ct);

            return Ok(rows);
        }

        [HttpGet("~/api/loxone/location-action/{locationCode}")]
        public async Task<IActionResult> Admin_GetLocationAction([FromRoute] string locationCode, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            try
            {
                var loc = await _loxDb.Locations
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Code == locationCode, ct);

                if (loc is null)
                    return NotFound(new { error = "LOCATION_NOT_FOUND", locationCode });

                var link = await _loxDb.LocationActions
                    .AsNoTracking()
                    .Include(x => x.LoxoneAction)
                    .SingleOrDefaultAsync(x => x.LocationId == loc.Id, ct);

                if (link is null)
                    return Ok(new { locationCode = loc.Code, locationId = loc.Id, action = (object?)null });

                return Ok(new
                {
                    locationCode = loc.Code,
                    locationId = loc.Id,
                    linkId = link.Id,
                    
                    isDefault = link.IsDefault,action = new
                    {
                        link.LoxoneAction.Id,
                        link.LoxoneAction.Code,
                        link.LoxoneAction.Name,
                        link.LoxoneAction.LoxoneCommand
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin_GetLocationAction failed. locationCode={LocationCode}", locationCode);

                return StatusCode(500, new
                {
                    error = "LOCATION_ACTION_READ_FAILED",
                    locationCode,
                    detail = ex.Message,
                    inner = ex.InnerException?.Message,
                    traceId = HttpContext.TraceIdentifier
                });
            }
        }


        [HttpPut("~/api/loxone/location-action/{locationCode}")]
        public async Task<IActionResult> Admin_SetLocationAction(
        [FromRoute] string locationCode,
        [FromBody] LocationActionSet req,
        CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            if (req is null)
                return BadRequest(new { error = "INVALID_BODY" });

            var actionCode = (req.ActionCode ?? "").Trim();
            if (string.IsNullOrWhiteSpace(actionCode))
                return BadRequest(new { error = "MISSING_ACTION_CODE" });

            var loc = await _loxDb.Locations.SingleOrDefaultAsync(x => x.Code == locationCode, ct);
            if (loc is null)
                return NotFound(new { error = "LOCATION_NOT_FOUND", locationCode });

            var act = await _loxDb.LoxoneActions.SingleOrDefaultAsync(x => x.Code == actionCode, ct);
            if (act is null)
                return NotFound(new { error = "ACTION_NOT_FOUND", actionCode });

            var link = await _loxDb.LocationActions.SingleOrDefaultAsync(x => x.LocationId == loc.Id, ct);
            var created = false;

            if (link is null)
            {
                link = new LocationAction
                {
                    LocationId = loc.Id,
                    LoxoneActionId = act.Id,

                    // IMPORTANT:
                    // - si el client envia IsDefault, el fem servir
                    // - si no envia res, per defecte = true (nou link)
                    IsDefault = req.IsDefault ?? true
                };

                _loxDb.LocationActions.Add(link);
                created = true;
            }
            else
            {
                link.LoxoneActionId = act.Id;

                
            if (req.IsDefault is not null) link.IsDefault = req.IsDefault.Value;
// Si el client envia IsDefault, actualitzem.
                // Si NO el envia (null), NO toquem el valor existent.
                if (req.IsDefault is not null)
                    link.IsDefault = req.IsDefault.Value;
            }

            try
            {
                await _loxDb.SaveChangesAsync(ct);

                return Ok(new
                {
                    ok = true,
                    locationCode = loc.Code,
                    locationId = loc.Id,
                    actionCode = act.Code,
                    actionId = act.Id,
                    linkId = link.Id,
                    isDefault = link.IsDefault,
                    mode = created ? "created" : "updated"
                });
            }
            catch (DbUpdateException ex)
            {
                var sqlite = ex.InnerException as SqliteException;

                _logger.LogError(ex,
                    "Admin_SetLocationAction SaveChanges failed. locationCode={LocationCode} locationId={LocationId} actionCode={ActionCode} actionId={ActionId} linkId={LinkId} created={Created} isDefault={IsDefault}",
                    loc.Code, loc.Id, act.Code, act.Id, link?.Id, created, link?.IsDefault);

                return StatusCode(500, new
                {
                    error = "LOCATION_ACTION_SAVE_FAILED",
                    locationCode = loc.Code,
                    locationId = loc.Id,
                    actionCode = act.Code,
                    actionId = act.Id,
                    linkId = link?.Id,
                    isDefault = link?.IsDefault,
                    mode = created ? "created" : "updated",
                    detail = ex.Message,
                    inner = ex.InnerException?.Message,
                    sqliteErrorCode = sqlite?.SqliteErrorCode,
                    sqliteExtendedErrorCode = sqlite?.SqliteExtendedErrorCode,
                    traceId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Admin_SetLocationAction failed. locationCode={LocationCode} locationId={LocationId} actionCode={ActionCode} actionId={ActionId} linkId={LinkId} created={Created} isDefault={IsDefault}",
                    loc.Code, loc.Id, act.Code, act.Id, link?.Id, created, link?.IsDefault);

                return StatusCode(500, new
                {
                    error = "LOCATION_ACTION_SAVE_FAILED",
                    locationCode = loc.Code,
                    locationId = loc.Id,
                    actionCode = act.Code,
                    actionId = act.Id,
                    linkId = link?.Id,
                    isDefault = link?.IsDefault,
                    mode = created ? "created" : "updated",
                    detail = ex.Message,
                    inner = ex.InnerException?.Message,
                    traceId = HttpContext.TraceIdentifier
                });
            }
        }



        [HttpDelete("~/api/loxone/location-action/{locationCode}")]
        public async Task<IActionResult> Admin_DeleteLocationAction([FromRoute] string locationCode, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            var loc = await _loxDb.Locations.SingleOrDefaultAsync(x => x.Code == locationCode, ct);
            if (loc is null)
                return NotFound(new { error = "LOCATION_NOT_FOUND", locationCode });

            var link = await _loxDb.LocationActions.SingleOrDefaultAsync(x => x.LocationId == loc.Id, ct);

            // Idempotent: si no hi ha vinculació, OK igualment
            if (link is null)
                return Ok(new { ok = true, locationCode = loc.Code, deleted = false });

            _loxDb.LocationActions.Remove(link);
            await _loxDb.SaveChangesAsync(ct);

            return Ok(new { ok = true, locationCode = loc.Code, deleted = true });
        }
    }
}


