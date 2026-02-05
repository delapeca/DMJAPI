using Microsoft.AspNetCore.Mvc;
using System;
using System.Data;
using System.Collections.Generic;
using XNDmjApi.Models.ClientProfile;
using XNDmjApi.Services;
using Microsoft.Extensions.Configuration;

namespace XNDmjApi.Controllers.IntranetSetup
{
    /// <summary>
    /// ClientsSelf (self-service) - STUBS.
    /// Objectiu: exposar rutes estables per a CLIENTS sense 404.
    /// Implementació (SQL/DI-API) es farà endpoint per endpoint.
    /// </summary>
    [ApiController]
    [Route("api/clients/self")]
    public class ClientsSelfController : ControllerBase
    {
        private readonly IConfiguration _cfg;

        
        private readonly ClientProfileService _clientProfileSvc = new ClientProfileService();
        
        private readonly ClientContactsService _clientContactsSvc = new ClientContactsService();

        private readonly ClientAddressesService _clientAddressesSvc = new ClientAddressesService();
public ClientsSelfController(IConfiguration cfg)
        {
            _cfg = cfg;
        }

        // Auth simple per API key (mateix patró Admin:ApiKey)
        private IActionResult? CheckApiKey()
        {
            var expected = (_cfg["Admin:ApiKey"] ?? "").Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(expected))
                return Unauthorized(new { ok = false, error = "APIKEY_NOT_CONFIGURED" });

            // 1) Intentem HEADER (tots els noms habituals)
            string key = "";
            if (Request.Headers.TryGetValue("AdminApiKey", out var ha)) key = ha.ToString();
            else if (Request.Headers.TryGetValue("X-Api-Key", out var h1)) key = h1.ToString();else if (Request.Headers.TryGetValue("X-API-Key", out var h2)) key = h2.ToString();
            else if (Request.Headers.TryGetValue("x-api-key", out var h3)) key = h3.ToString();
            else if (Request.Headers.TryGetValue("api_key", out var h4)) key = h4.ToString();          // Swagger scheme antic
            else if (Request.Headers.TryGetValue("Api-Key", out var h5)) key = h5.ToString();
            else if (Request.Headers.TryGetValue("Authorization", out var h6)) key = h6.ToString();

            key = (key ?? "").Trim().Trim('"');
            if (key.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                key = key.Substring(7).Trim().Trim('"');

            // 2) Fallback: QUERY STRING (per si Swagger ho envia com a parameter ?api_key=...)
            if (string.IsNullOrWhiteSpace(key))
            {
                key = (Request.Query["api_key"].ToString() ?? "").Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(key))
                    key = (Request.Query["apiKey"].ToString() ?? "").Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(key))
                    key = (Request.Query["X-Api-Key"].ToString() ?? "").Trim().Trim('"');
            }

            if (string.IsNullOrWhiteSpace(key))
                return Unauthorized(new { ok = false, error = "MISSING_API_KEY" });

            if (!string.Equals(key, expected, StringComparison.Ordinal))
                return Unauthorized(new { ok = false, error = "INVALID_API_KEY" });

            return null;
        }


        private IActionResult NotImpl(string endpoint)
            => StatusCode(501, new { ok = false, error = "NOT_IMPLEMENTED", endpoint });
        // 1) GET /api/clients/self/profile
        [HttpGet("profile")]
        public IActionResult GetProfile([FromQuery] string? cardCode)
        {
            var fail = CheckApiKey(); if (fail != null) return fail;

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

        // 2) GET /api/clients/self/contacts
        [HttpGet("contacts")]
        public IActionResult GetContacts([FromQuery] string? cardCode)
        {
            var fail = CheckApiKey(); if (fail != null) return fail;

            // CardCode: query (?cardCode=...) o header X-CardCode (fallback)
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

                        string sName   = table.Columns.Contains("Name")   && row["Name"]   != DBNull.Value ? Convert.ToString(row["Name"])   ?? "" : "";
                        string sRole   = table.Columns.Contains("Role")   && row["Role"]   != DBNull.Value ? Convert.ToString(row["Role"])   ?? "" : "";
                        string sEmail  = table.Columns.Contains("Email")  && row["Email"]  != DBNull.Value ? Convert.ToString(row["Email"])  ?? "" : "";
                        string sPhone  = table.Columns.Contains("Phone")  && row["Phone"]  != DBNull.Value ? Convert.ToString(row["Phone"])  ?? "" : "";
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

        // 3) GET /api/clients/self/addresses
        [HttpGet("addresses")]
        public IActionResult GetAddresses([FromQuery] string? cardCode)
        {
            var fail = CheckApiKey(); if (fail != null) return fail;

            // CardCode: query (?cardCode=...) o header X-CardCode (fallback)
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
                        string t  = table.Columns.Contains("AddressType") && row["AddressType"] != DBNull.Value ? Convert.ToString(row["AddressType"]) ?? "" : "";

                        string street   = table.Columns.Contains("Street")   && row["Street"]   != DBNull.Value ? Convert.ToString(row["Street"])   ?? "" : "";
                        string zip      = table.Columns.Contains("ZipCode")  && row["ZipCode"]  != DBNull.Value ? Convert.ToString(row["ZipCode"])  ?? "" : "";
                        string city     = table.Columns.Contains("City")     && row["City"]     != DBNull.Value ? Convert.ToString(row["City"])     ?? "" : "";
                        string province = table.Columns.Contains("Province") && row["Province"] != DBNull.Value ? Convert.ToString(row["Province"]) ?? "" : "";
                        string country  = table.Columns.Contains("Country")  && row["Country"]  != DBNull.Value ? Convert.ToString(row["Country"])  ?? "" : "";

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
// 4) GET /api/clients/self/balances
        [HttpGet("balances")]
        public IActionResult GetBalances([FromQuery] string? cardCode)
        {
            var fail = CheckApiKey(); if (fail != null) return fail;

            // CardCode: query (?cardCode=...) o header X-CardCode (fallback)
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
// 5) PATCH /api/clients/self/phones
        public sealed class PhonesPatchRequest
        {
            public string? Fixed { get; set; }
            public string? Mobile { get; set; }
        }

        [HttpPatch("phones")]
        public IActionResult PatchPhones([FromBody] PhonesPatchRequest req)
        {
            var fail = CheckApiKey(); if (fail != null) return fail;
            return NotImpl("PATCH /api/clients/self/phones");
        }

        // 6) POST /api/clients/self/changerequests/contacts
        // 7) POST /api/clients/self/changerequests/addresses
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
            var fail = CheckApiKey(); if (fail != null) return fail;
            return NotImpl("POST /api/clients/self/changerequests/contacts");
        }

        [HttpPost("changerequests/addresses")]
        public IActionResult PostChangeRequestAddresses([FromBody] ChangeRequestRequest req)
        {
            var fail = CheckApiKey(); if (fail != null) return fail;
            return NotImpl("POST /api/clients/self/changerequests/addresses");
        }
    }
}







