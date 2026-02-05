using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using SAPbobsCOM;
using System.Runtime.InteropServices;
using System.ComponentModel.DataAnnotations;
using XNDmjApi.Functions;
using XNDmjApi.Services;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BusinessPartnersController : ControllerBase
    {
        private readonly IConfiguration _config;
        string ConnectionStringDOMENJO = null;

        Funcions Funcions = new Funcions();
        SAPLoginService login = new SAPLoginService();

        SqlConnection _connSQL;

        
        public BusinessPartnersController(IConfiguration configuration)
        {
            _config = configuration;
            //ConnectionStringDOMENJO = _config["Logging:BBDD:MSSQL"];
        }

        // POST api/<BusinessPartnersController>
        [HttpPost("GetBusinessPartners")]
        public ActionResult GetBusinessPartners([FromForm] string userToken, [FromForm] string cardType, [FromForm] string validFor, [FromForm] string groupCode, [FromForm] string cardCode = "%")
        {
            // cardType: C=Client, S=Supplier
            // validFor: Y=Actiu, N=Inactiu
            // GroupCode: 101=proveidor, 102=Creditor, 111=Constructor, 
            if (!login.ValidateUserToken(userToken)) return BadRequest("Toquen caducat");

            string query = Funcions.GetQuery("GetBusinessPartners.sql");
            query += $"WHERE CardCode like '{cardCode}' and CardType like '{cardType}' and ValidFor like '{validFor}' and GroupCode={int.Parse(groupCode)} order by CardName;";

            string[] parametres = { 
                $"CardType:'{cardType}'",
                $"ValidFor:'{validFor}'",
                $"GroupCode:{int.Parse(groupCode)}",
                $"CardCode:'{cardCode}'"
            };


            var dt = DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, query, parametres);

            var result = JsonConvert.SerializeObject(dt);

            return Ok(result);
        }

        [HttpPost("GetBusinessPartner")]
        public ActionResult GetBusinessPartner([FromForm] string userToken, [FromForm] string cardCode)
        {
            // cardType: C=Client, S=Supplier
            // validFor: Y=Actiu, N=Inactiu
            // GroupCode: 101=proveidor, 102=Creditor, 111=Constructor, 
            if (!login.ValidateUserToken(userToken)) return BadRequest("Toquen caducat");

            string query = Funcions.GetQuery("GetBusinessPartners.sql");
            query += $"WHERE CardCode='{cardCode}';";

            string[] parametres = {
                $"CardCode:{cardCode}"
            };


            var dt = DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, query, parametres);

            var result = JsonConvert.SerializeObject(dt);

            return Ok(result);
        }
    
        // POST api/BusinessPartners/UpdateBusinessPartner
        // ✅ Write (DI-API) - restringit a grup SAP Admin/Advanced
        [HttpPost("UpdateBusinessPartner")]
        public ActionResult UpdateBusinessPartner(
            [FromForm] string userToken,
            [FromForm] string cardCode,
            [FromForm] string? phone1 = null,
            [FromForm] string? cellular = null,
            [FromForm] string? email = null,
            [FromForm] string? contactPerson = null,
            [FromForm] string? fax = null,
            [FromForm] string? notes = null)
        {
            if (!login.ValidateUserToken(userToken))
                return BadRequest(new { ok = false, error = "TOKEN_EXPIRED" });

            // Autorització mínima per evitar vulnerabilitat: només Admin/Advanced
            if (!login.IsAdminOrAdvancedFromToken(userToken))
                return StatusCode(403, new { ok = false, error = "NOT_AUTHORIZED" });

            var svc = new SapBusinessPartnerService();
            var (ok, code, message) = svc.UpdateBusinessPartner(
                cardCode,
                phone1,
                cellular,
                email,
                contactPerson,
                fax,
                notes
            );

            if (!ok)
                return BadRequest(new { ok = false, error = code, message });

            return Ok(new { ok = true, code, message });
        }

        // =========================
        // Contacts (DI-API) · Intranet only (Admin/Advanced)
        // El client NO pot tocar directament OCPR.
        // =========================

        public class ContactCreateRequest
        {
            [Required] public string userToken { get; set; }
            [Required] public string cardCode { get; set; }

            // CREATE fields (OCPR)
            [Required] public string name { get; set; }
            public string? address { get; set; }
            public string? tel1 { get; set; }
            public string? tel2 { get; set; }
            public string? cellular { get; set; }
            public string? email { get; set; }
            public string? firstName { get; set; }
            public string? middleName { get; set; }
            public string? lastName { get; set; }

            // UI-friendly flag (maps internally to U_BOY_85_ECAT)
            public bool? receiveSalesDocs { get; set; }
        }

        public class ContactUpdateRequest
        {
            [Required] public string userToken { get; set; }
            [Required] public string cardCode { get; set; }
            [Required] public int id { get; set; }

            // UPDATE allowed fields only
            public string? address { get; set; }
            public string? tel1 { get; set; }
            public string? tel2 { get; set; }
            public string? cellular { get; set; }
            public string? email { get; set; }

            // UI-friendly flag (maps internally to U_BOY_85_ECAT)
            public bool? receiveSalesDocs { get; set; }
        }

        // POST api/BusinessPartners/CreateBusinessPartnerContact
        // ✅ Write (DI-API) - restringit a grup SAP Admin/Advanced
        [HttpPost("CreateBusinessPartnerContact")]
        public ActionResult CreateBusinessPartnerContact([FromBody] ContactCreateRequest req)
        {
            if (req == null) return BadRequest(new { ok = false, error = "MISSING_BODY" });

            if (!login.ValidateUserToken(req.userToken))
                return BadRequest(new { ok = false, error = "TOKEN_EXPIRED" });

            if (!login.IsAdminOrAdvancedFromToken(req.userToken))
                return StatusCode(403, new { ok = false, error = "NOT_AUTHORIZED" });

            var svc = new SapBusinessPartnerContactService();
            var (ok, code, message, contactId) = svc.CreateContact(
                req.cardCode,
                req.name,
                req.address,
                req.tel1,
                req.tel2,
                req.cellular,
                req.email,
                req.firstName,
                req.middleName,
                req.lastName,
                req.receiveSalesDocs
            );

            if (!ok)
                return BadRequest(new { ok = false, error = code, message });

            return Ok(new { ok = true, code, message, contactId });
        }

        // POST api/BusinessPartners/UpdateBusinessPartnerContact
        // ✅ Write (DI-API) - restringit a grup SAP Admin/Advanced
        [HttpPost("UpdateBusinessPartnerContact")]
        public ActionResult UpdateBusinessPartnerContact([FromBody] ContactUpdateRequest req)
        {
            if (req == null) return BadRequest(new { ok = false, error = "MISSING_BODY" });

            if (!login.ValidateUserToken(req.userToken))
                return BadRequest(new { ok = false, error = "TOKEN_EXPIRED" });

            if (!login.IsAdminOrAdvancedFromToken(req.userToken))
                return StatusCode(403, new { ok = false, error = "NOT_AUTHORIZED" });

            var svc = new SapBusinessPartnerContactService();
            var (ok, code, message) = svc.UpdateContact(
                req.cardCode,
                req.id,
                req.address,
                req.tel1,
                req.tel2,
                req.cellular,
                req.email,
                req.receiveSalesDocs
            );

            if (!ok)
                return BadRequest(new { ok = false, error = code, message });

            return Ok(new { ok = true, code, message });
        }
}
}


