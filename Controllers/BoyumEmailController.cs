using Microsoft.AspNetCore.Mvc;
using System.Data;
using XNDmjApi.Functions;
using XNDmjApi.Services;

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BoyumEmailController : ControllerBase
    {
        private readonly BoyumEmailService _boyumService = new BoyumEmailService();
        private readonly SAPLoginService _loginService = new SAPLoginService();

        [HttpPost("sendDocumentEmail")]
        public ActionResult SendDocumentEmail(
            [FromForm] string userToken,
            [FromForm] int docEntry,
            [FromForm] int objectType,
            [FromForm] string? emailAddress = null)
        {
            // Validar token
            if (!_loginService.ValidateUserToken(userToken))
                return Unauthorized("Token caducat");

            // Enviar email
            var result = _boyumService.SendDocumentEmail(docEntry, objectType, emailAddress);

            if (result.Success)
                return Ok(new { success = true, message = result.Message });
            else
                return BadRequest(new { success = false, error = result.Message });
        }

        [HttpPost("checkBoyumSP")]
        public ActionResult CheckBoyumSP([FromForm] string userToken)
        {
            if (!_loginService.ValidateUserToken(userToken))
                return Unauthorized("Token caducat");

            string query = @"
                SELECT ROUTINE_NAME 
                FROM INFORMATION_SCHEMA.ROUTINES 
                WHERE ROUTINE_TYPE = 'PROCEDURE' 
                  AND ROUTINE_NAME LIKE '%BOY%'
                ORDER BY ROUTINE_NAME
            ";

            try
            {
                DataTable dt = DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, query, new string[] { });
                var result = new List<string>();

                foreach (DataRow row in dt.Rows)
                {
                    result.Add(row["ROUTINE_NAME"].ToString());
                }

                return Ok(new { procedures = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}