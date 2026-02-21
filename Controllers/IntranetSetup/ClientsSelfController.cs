using Microsoft.AspNetCore.Mvc;
using System;
using System.Data;
using System.Collections.Generic;
using XNDmjApi.Models.ClientProfile;
using XNDmjApi.Services;
using Microsoft.Extensions.Configuration;
using XNDmjApi.Infrastructure.ApiKeys;
using XNDmjApi.Functions;

namespace XNDmjApi.Controllers.IntranetSetup
{
    /// <summary>
    /// ClientsSelf (self-service)
    /// ==========================
    /// Objectiu:
    ///   Exposar rutes estables per a CLIENTS (sense 404) i anar implementant-les endpoint per endpoint.
    ///
    /// IMPORTANT (migració a ProfileApiKey):
    ///   Aquests endpoints han de treballar amb la BD seleccionada pel "perfil" resolt via ApiKeyProfileMiddleware.
    ///   - Header nou:      ProfileApiKey
    ///   - Header legacy:   X-Api-Key (fallback temporal)
    ///
    /// Flux correcte (patró a replicar a TOTS els mètodes d’aquest controller):
    ///   1) RequireProfileAndSelectDb(out profile)
    ///   2) Validació de paràmetres (cardCode, etc.)
    ///   3) Crida al servei SQL (ClientProfileService / Contacts / Addresses...)
    ///
    /// NOTA IMPORTANT (arquitectura actual):
    ///   Dades.* és estàtic/global → canviar BD per request pot tenir risc si hi ha concurrència amb perfils diferents.
    ///   Però NO ho toquem ara (regla del projecte): només repliquem el patró existent.
    /// </summary>
    [ApiController]
    [Route("api/clients/self")]
    public class ClientsSelfController : ControllerBase
    {
        private readonly IConfiguration _cfg;

        // Serveis SQL (fan servir Dades.ConnectionStringDOMENJO)
        private readonly ClientProfileService _clientProfileSvc = new ClientProfileService();
        private readonly ClientContactsService _clientContactsSvc = new ClientContactsService();
        private readonly ClientAddressesService _clientAddressesSvc = new ClientAddressesService();

        public ClientsSelfController(IConfiguration cfg)
        {
            _cfg = cfg;
        }

        // =====================================================================
        // LEGACY (ANUL·LAT) - Admin:ApiKey
        // ---------------------------------------------------------------------
        // NOTE (DMJ): Aquest CheckApiKey() era el patró antic (Admin:ApiKey).
        // Ara /api/clients/self va amb ProfileApiKey via ApiKeyProfileMiddleware,
        // perquè això permet:
        //   - Selecció de BD segons ApiKey (PROD/TEST / CompanyDb)
        //   - Mateix patró que SapChangeRequestsUdo
        //
        // Si algun dia cal revertir temporalment (NO recomanat), es pot reactivar,
        // però llavors PERDS la selecció de BD per perfil.
        // =====================================================================
        //private IActionResult? CheckApiKey()
        //{
        //    var expected = (_cfg["Admin:ApiKey"] ?? "").Trim().Trim('"');
        //
        //    if (string.IsNullOrWhiteSpace(expected))
        //        return Unauthorized(new { ok = false, error = "APIKEY_NOT_CONFIGURED" });
        //
        //    // 1) Intentem HEADER (tots els noms habituals)
        //    string key = "";
        //    if (Request.Headers.TryGetValue("AdminApiKey", out var ha)) key = ha.ToString();
        //    else if (Request.Headers.TryGetValue("X-Api-Key", out var h1)) key = h1.ToString();
        //    else if (Request.Headers.TryGetValue("X-API-Key", out var h2)) key = h2.ToString();
        //    else if (Request.Headers.TryGetValue("x-api-key", out var h3)) key = h3.ToString();
        //    else if (Request.Headers.TryGetValue("api_key", out var h4)) key = h4.ToString();          // Swagger scheme antic
        //    else if (Request.Headers.TryGetValue("Api-Key", out var h5)) key = h5.ToString();
        //    else if (Request.Headers.TryGetValue("Authorization", out var h6)) key = h6.ToString();
        //
        //    key = (key ?? "").Trim().Trim('"');
        //    if (key.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        //        key = key.Substring(7).Trim().Trim('"');
        //
        //    // 2) Fallback: QUERY STRING (per si Swagger ho envia com a parameter ?api_key=...)
        //    if (string.IsNullOrWhiteSpace(key))
        //    {
        //        key = (Request.Query["api_key"].ToString() ?? "").Trim().Trim('"');
        //        if (string.IsNullOrWhiteSpace(key))
        //            key = (Request.Query["apiKey"].ToString() ?? "").Trim().Trim('"');
        //        if (string.IsNullOrWhiteSpace(key))
        //            key = (Request.Query["X-Api-Key"].ToString() ?? "").Trim().Trim('"');
        //    }
        //
        //    if (string.IsNullOrWhiteSpace(key))
        //        return Unauthorized(new { ok = false, error = "MISSING_API_KEY" });
        //
        //    if (!string.Equals(key, expected, StringComparison.Ordinal))
        //        return Unauthorized(new { ok = false, error = "INVALID_API_KEY" });
        //
        //    return null;
        //}

        private IActionResult NotImpl(string endpoint)
            => StatusCode(501, new { ok = false, error = "NOT_IMPLEMENTED", endpoint });

        // =====================================================================
        // 1) GET /api/clients/self/profile
        // =====================================================================
        [HttpGet("profile")]
        public IActionResult GetProfile([FromQuery] string? cardCode)
        {
            // PAS 1 (OBLIGATORI): resol perfil i selecciona BD segons ProfileApiKey (o fallback X-Api-Key)
            var fail = RequireProfileAndSelectDb(out var profile);
            if (fail != null) return fail;

            // CardCode: query (?cardCode=...) o header X-CardCode (fallback)
            var cc = (cardCode ?? "").Trim();
            if (string.IsNullOrWhiteSpace(cc))
                cc = (Request.Headers["X-CardCode"].ToString() ?? "").Trim();

            if (string.IsNullOrWhiteSpace(cc))
                return BadRequest(new { ok = false, error = "MISSING_CARDCODE" });

            try
            {
                var dto = _clientProfileSvc.GetProfile(cc);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ok = false, error = "ERROR", message = ex.Message });
            }
        }

        // =====================================================================
        // 2) GET /api/clients/self/contacts
        // =====================================================================
        [HttpGet("contacts")]
        public IActionResult GetContacts([FromQuery] string? cardCode)
        {
            var fail = RequireProfileAndSelectDb(out var profile);
            if (fail != null) return fail;

            var cc = (cardCode ?? "").Trim();
            if (string.IsNullOrWhiteSpace(cc))
                cc = (Request.Headers["X-CardCode"].ToString() ?? "").Trim();

            if (string.IsNullOrWhiteSpace(cc))
                return BadRequest(new { ok = false, error = "MISSING_CARDCODE" });

            try
            {
                var table = _clientContactsSvc.GetContacts(cc);

                var contacts = new List<object>();
                if (table != null)
                {
                    foreach (DataRow row in table.Rows)
                    {
                        int id = 0;
                        if (table.Columns.Contains("Id") && row["Id"] != DBNull.Value)
                            int.TryParse(Convert.ToString(row["Id"]), out id);

                        string sName = table.Columns.Contains("Name") && row["Name"] != DBNull.Value ? Convert.ToString(row["Name"]) ?? "" : "";
                        string sRole = table.Columns.Contains("Role") && row["Role"] != DBNull.Value ? Convert.ToString(row["Role"]) ?? "" : "";
                        string sEmail = table.Columns.Contains("Email") && row["Email"] != DBNull.Value ? Convert.ToString(row["Email"]) ?? "" : "";
                        string sPhone = table.Columns.Contains("Phone") && row["Phone"] != DBNull.Value ? Convert.ToString(row["Phone"]) ?? "" : "";
                        string sMobile = table.Columns.Contains("Mobile") && row["Mobile"] != DBNull.Value ? Convert.ToString(row["Mobile"]) ?? "" : "";

                        bool active = true;
                        if (table.Columns.Contains("Active") && row["Active"] != DBNull.Value)
                        {
                            var a = Convert.ToString(row["Active"]) ?? "1";
                            active = (a == "1" || a.Equals("true", StringComparison.OrdinalIgnoreCase) || a.Equals("Y", StringComparison.OrdinalIgnoreCase));
                        }

                        contacts.Add(new
                        {
                            id = id,
                            name = sName,
                            role = sRole,
                            email = sEmail,
                            phone = sPhone,
                            mobile = sMobile,
                            active = active
                        });
                    }
                }

                return Ok(new { ok = true, cardCode = cc, contacts = contacts });
            }
            catch (Exception ex)
            {
                return BadRequest(new { ok = false, error = "ERROR", message = ex.Message });
            }
        }

        // =====================================================================
        // 3) GET /api/clients/self/addresses
        // =====================================================================
        [HttpGet("addresses")]
        public IActionResult GetAddresses([FromQuery] string? cardCode)
        {
            var fail = RequireProfileAndSelectDb(out var profile);
            if (fail != null) return fail;

            var cc = (cardCode ?? "").Trim();
            if (string.IsNullOrWhiteSpace(cc))
                cc = (Request.Headers["X-CardCode"].ToString() ?? "").Trim();

            if (string.IsNullOrWhiteSpace(cc))
                return BadRequest(new { ok = false, error = "MISSING_CARDCODE" });

            try
            {
                var table = _clientAddressesSvc.GetAddresses(cc);

                var addresses = new List<object>();
                if (table != null)
                {
                    foreach (DataRow row in table.Rows)
                    {
                        string id = table.Columns.Contains("AddressId") && row["AddressId"] != DBNull.Value ? Convert.ToString(row["AddressId"]) ?? "" : "";
                        string t = table.Columns.Contains("AddressType") && row["AddressType"] != DBNull.Value ? Convert.ToString(row["AddressType"]) ?? "" : "";

                        string street = table.Columns.Contains("Street") && row["Street"] != DBNull.Value ? Convert.ToString(row["Street"]) ?? "" : "";
                        string zip = table.Columns.Contains("ZipCode") && row["ZipCode"] != DBNull.Value ? Convert.ToString(row["ZipCode"]) ?? "" : "";
                        string city = table.Columns.Contains("City") && row["City"] != DBNull.Value ? Convert.ToString(row["City"]) ?? "" : "";
                        string province = table.Columns.Contains("Province") && row["Province"] != DBNull.Value ? Convert.ToString(row["Province"]) ?? "" : "";
                        string country = table.Columns.Contains("Country") && row["Country"] != DBNull.Value ? Convert.ToString(row["Country"]) ?? "" : "";

                        bool isDefault = false;
                        if (table.Columns.Contains("IsDefault") && row["IsDefault"] != DBNull.Value)
                        {
                            var a = Convert.ToString(row["IsDefault"]) ?? "0";
                            isDefault = (a == "1" || a.Equals("true", StringComparison.OrdinalIgnoreCase) || a.Equals("Y", StringComparison.OrdinalIgnoreCase));
                        }

                        addresses.Add(new
                        {
                            id = id,
                            type = t,          // B / S
                            name = id,         // etiqueta d'adreça SAP (Address)
                            street = street,
                            zip = zip,
                            city = city,
                            province = province,
                            country = country,
                            isDefault = isDefault
                        });
                    }
                }

                return Ok(new { ok = true, cardCode = cc, addresses = addresses });
            }
            catch (Exception ex)
            {
                return BadRequest(new { ok = false, error = "ERROR", message = ex.Message });
            }
        }

        // =====================================================================
        // 4) GET /api/clients/self/balances
        // =====================================================================
        [HttpGet("balances")]
        public IActionResult GetBalances([FromQuery] string? cardCode)
        {
            var fail = RequireProfileAndSelectDb(out var profile);
            if (fail != null) return fail;

            var cc = (cardCode ?? "").Trim();
            if (string.IsNullOrWhiteSpace(cc))
                cc = (Request.Headers["X-CardCode"].ToString() ?? "").Trim();

            if (string.IsNullOrWhiteSpace(cc))
                return BadRequest(new { ok = false, error = "MISSING_CARDCODE" });

            try
            {
                var dto = _clientProfileSvc.GetProfile(cc);

                return Ok(new
                {
                    ok = true,
                    cardCode = (dto.CardCode ?? cc),
                    balances = new
                    {
                        account = dto.BalanceAccount,
                        orders = dto.BalanceOrders,
                        deliveries = dto.BalanceDeliveries
                    },
                    asOf = DateTime.UtcNow.ToString("yyyy-MM-dd")
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { ok = false, error = "ERROR", message = ex.Message });
            }
        }

        // =====================================================================
        // 5) PATCH /api/clients/self/phones (encara NOT_IMPLEMENTED)
        // =====================================================================
        public sealed class PhonesPatchRequest
        {
            public string? Fixed { get; set; }
            public string? Mobile { get; set; }
        }

        [HttpPatch("phones")]
        public IActionResult PatchPhones([FromBody] PhonesPatchRequest req)
        {
            var fail = RequireProfileAndSelectDb(out var profile);
            if (fail != null) return fail;

            return NotImpl("PATCH /api/clients/self/phones");
        }

        // =====================================================================
        // 6) POST /api/clients/self/changerequests/contacts (NOT_IMPLEMENTED)
        // 7) POST /api/clients/self/changerequests/addresses (NOT_IMPLEMENTED)
        // =====================================================================
        public sealed class ChangeRequestRequest
        {
            public string? Kind { get; set; }   // contact|address (o el que definim després)
            public string? Action { get; set; } // update|add|delete
            public object? Target { get; set; }
            public string? Reason { get; set; }
            public object? RequestedBy { get; set; }
        }

        [HttpPost("changerequests/contacts")]
        public IActionResult PostChangeRequestContacts([FromBody] ChangeRequestRequest req)
        {
            var fail = RequireProfileAndSelectDb(out var profile);
            if (fail != null) return fail;

            return NotImpl("POST /api/clients/self/changerequests/contacts");
        }

        [HttpPost("changerequests/addresses")]
        public IActionResult PostChangeRequestAddresses([FromBody] ChangeRequestRequest req)
        {
            var fail = RequireProfileAndSelectDb(out var profile);
            if (fail != null) return fail;

            return NotImpl("POST /api/clients/self/changerequests/addresses");
        }

        // =====================================================================
        // Helper central: Perfil + Selecció de BD
        // ---------------------------------------------------------------------
        // QUAN AFEGEIXIS UN NOU ENDPOINT a /api/clients/self:
        //   1) Copia aquestes 2 línies al principi del mètode:
        //        var fail = RequireProfileAndSelectDb(out var profile);
        //        if (fail != null) return fail;
        //
        // I ja tens:
        //   - Auth per ProfileApiKey (amb fallback X-Api-Key)
        //   - Dades.DOMENJO_BBDD ajustat
        //   - Dades.ConnectionStringDOMENJO recalculada amb SetupDades()
        //
        // ON ES RESOL EL PERFIL?
        //   - ApiKeyProfileMiddleware posa ApiKeyProfile a HttpContext.Items[ApiKeyProfileMiddleware.HttpContextItemKey]
        // =====================================================================
        private IActionResult? RequireProfileAndSelectDb(out ApiKeyProfile? profile)
        {
            // 1) Recupera perfil resolt pel middleware (si no hi és, és que falta ApiKey o és invàlida)
            profile = HttpContext.Items[ApiKeyProfileMiddleware.HttpContextItemKey] as ApiKeyProfile;
            if (profile == null)
            {
                return Unauthorized(new
                {
                    ok = false,
                    code = "MISSING_PROFILE",
                    message = "Falta perfil (ProfileApiKey o X-Api-Key)."
                });
            }

            // 2) CompanyDb és el “selector” de la BD SAP / SQL a consultar
            var db = (profile.CompanyDb ?? "").Trim();
            if (string.IsNullOrWhiteSpace(db))
            {
                return StatusCode(500, new
                {
                    ok = false,
                    code = "DB_CONTEXT_MISSING",
                    message = "El perfil no porta CompanyDb."
                });
            }

            // 3) Inicialitza / canvia DB context abans de cridar serveis SQL.
            //    IMPORTANT: ClientProfileService.EnsureConnection() només “força SBO_DOMENJO”
            //               si Dades.ConnectionStringDOMENJO està buida.
            //               Aquí ens assegurem que la connection string quedi ben fixada abans.
            try
            {
                // Recalcula només si cal:
                //  - si canvia el DB
                //  - o si encara no tenim ConnectionStringDOMENJO
                if (!string.Equals(Dades.DOMENJO_BBDD ?? "", db, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(Dades.ConnectionStringDOMENJO))
                {
                    Dades.DOMENJO_BBDD = db;
                    Dades.SetupDades();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    ok = false,
                    code = "DB_SELECT_FAILED",
                    message = ex.Message
                });
            }

            return null; // OK
        }
    }
}
