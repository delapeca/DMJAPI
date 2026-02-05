using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using XNDmjApi.Functions;
using XNDmjApi.Services;

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SAPLoginController : ControllerBase
    {
        SAPLoginService login = new SAPLoginService();

        [HttpPost("getLoginToken")]
        public ActionResult getLoginToken([FromForm] string user, [FromForm] string password, [FromForm] string bbdd)
        {

            string loginToken = login.GetLoginToken(user, password, bbdd);

            return Ok(loginToken);
        }

        [HttpPost("loginByToken")]
        public ActionResult LoginByToken([FromForm] string loginToken)
        {
            
            string userToken = login.LoginByToken(loginToken);

            if (userToken == null || userToken == "-1")
                return BadRequest("Token caducat");

            return Ok(userToken);
        }

        [HttpPost("getUserInfo")]
        public ActionResult getUserInfo([FromForm] string userToken)
        {
            if (!login.ValidateUserToken(userToken)) return BadRequest("Token caducat");

            Encryption encrypt = new Encryption();
            string tokenDecoded = encrypt.AES256_Decrypt(encrypt.AES256_USER_Key, userToken);
            string user = tokenDecoded.Split('#')[0];

            string token = login.GetUserInfo(user);

            return Ok(token);
        }
    }
}
