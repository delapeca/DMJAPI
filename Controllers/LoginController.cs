using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using SAPbobsCOM;
using System.Data;
using XNDmjApi.Functions;
using XNDmjApi.Models;
using Newtonsoft.Json; // assegura't que això és a dalt del fitxer
//using XNDmjApi.Functions;


// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        Funcions Funcions = new Funcions();

        // Funció inhabilitada ja que aquesta funció s'ha de fer al costat del client
        [HttpPost("getTokenLogin")]
        public ActionResult GetTokenLogin([FromForm] string email, [FromForm] string password)
        {
            Login login = new Login();
            return Ok(login.getTokenLogin(email, password));
        }

        [HttpPost("loginByToken")]
        public ActionResult LoginByToken([FromForm] string loginToken,[FromForm] string role = null)
        {
            try
            {
                var login = new Login();
                string token = login.LoginByToken(loginToken, role);

                return Ok(token);
            }
            catch (Exception ex)
            {
                // Això farà que Laravel vegi el missatge a "Body: ..."
                return StatusCode(500, $"LoginByToken error: {ex.Message}");
            }
        }

        [HttpPost("createNewUser")]
        public ActionResult CreateNewUser([FromForm] string token)
        {
            try
            {
                var login = new Login();

                // Ara Login.CreateNewUser(token) ja retorna un JSON del tipus:
                // { "success": true/false, "code": "...", "message": "..." }
                string resultat = login.CreateNewUser(token);

                // Cas: resposta buida → error tècnic
                if (string.IsNullOrWhiteSpace(resultat))
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        code = "EMPTY_RESPONSE",
                        message = "CreateNewUser ha retornat una resposta buida."
                    });
                }

                // Intentem parsejar el JSON per validar-lo
                try
                {
                    var data = JsonConvert.DeserializeObject<object>(resultat);

                    // ✅ Retornem sempre 200 amb el JSON estructurat.
                    // Laravel mirarà 'success' per saber si és OK o error funcional.
                    return Ok(data);
                }
                catch
                {
                    // Si no és JSON vàlid, enviem error 500 amb el brut
                    return StatusCode(500, new
                    {
                        success = false,
                        code = "INVALID_JSON",
                        message = "Resposta no vàlida de Login.CreateNewUser.",
                        raw = resultat
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    code = "EXCEPTION",
                    message = "Error intern a CreateNewUser",
                    detail = ex.Message
                });
            }
        }

        [HttpPost("setPassword")]
        public ActionResult SetPassword([FromForm] string userToken, [FromForm] string encriptedOldPassword, [FromForm] string encriptedNewPassword)
        {
            //Clases.Log.LogWrite($"SetPassword: token={token}, encriptedOldPassword={encriptedOldPassword}, encriptedNewPassword={encriptedNewPassword}");
            Login login = new Login();

            //// Comprovem que el userToken sigui valid
            //if (!login.ValidateUserToken(userToken)) return BadRequest("Toquen caducat"); // Ja es valida en el seguent pas

            bool resultado = login.SetPassword(userToken, encriptedOldPassword, encriptedNewPassword);
            if (resultado)
                return Ok(resultado);
            else
                return BadRequest(resultado);
        }

        [HttpPost("resetPasswordByToken")]
        public ActionResult ResetPasswordByToken([FromForm] string resetToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(resetToken))
                    return BadRequest(new { ok = false, code = "MISSING_TOKEN", message = "Falta resetToken." });

                Login login = new Login();
                string r = login.ResetPasswordByToken(resetToken);

                if (r == "OK")
                    return Ok(new { ok = true });

                if (r == "TOKEN_EXPIRED")
                    return BadRequest(new { ok = false, code = "TOKEN_EXPIRED", message = "El token ha caducat." });

                if (r == "TOKEN_INVALID")
                    return BadRequest(new { ok = false, code = "TOKEN_INVALID", message = "Token invàlid." });

                if (r == "USER_NOT_FOUND")
                    return NotFound(new { ok = false, code = "USER_NOT_FOUND", message = "Usuari no trobat." });

                if (r == "DB_CONTEXT_MISSING")
                    return StatusCode(500, new { ok = false, code = "DB_CONTEXT_MISSING", message = "Falta context de DB." });

                return BadRequest(new { ok = false, code = "RESET_FAILED", message = r });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { ok = false, code = "EXCEPTION", message = ex.Message });
            }
        }

        [HttpPost("getUserInfo")]
        public ActionResult GetUserInfo([FromForm] string userToken)
        {
            Login login = new Login();

            // Comprovem que el userToken sigui valid
            if (!login.ValidateUserToken(userToken)) return BadRequest("Toquen caducat");

            User user = login.GetUserInfo(userToken);
            // Clases.Log.LogWrite($"Logout");
            var userInfoJson = JsonConvert.SerializeObject(user);
            return Ok(userInfoJson);
        }

        [HttpPost("logout")]
        public ActionResult logout([FromForm] string token)
        {
           // Clases.Log.LogWrite($"Logout");
            return Ok("");
        }
    }
}
