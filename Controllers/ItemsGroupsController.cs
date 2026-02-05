using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using XNDmjApi.Functions;
using XNDmjApi.Services;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ItemsGroupsController : ControllerBase
    {
        Login login = new Login();

        // POST api/<ItemsGroupsController>
        [HttpPost("getItemGroups")]
        public ActionResult getItemGroups()
        {
            ItemsGroupsService SItemsGroups = new ItemsGroupsService();
            string jsonResult = SItemsGroups.GetItemsGroups();

            if (jsonResult != null)
                return Ok(jsonResult);
            else
                return BadRequest(jsonResult);
            return Ok();
            //return BadRequest();
        }

        [HttpPost("getItemSubGroups")]
        public ActionResult getItemSubGroups([FromForm] string Familia)
        {
            ItemsGroupsService SItemsGroups = new ItemsGroupsService();
            string jsonResult = SItemsGroups.GetItemsSubGroups(Familia);

            if (jsonResult != null)
                return Ok(jsonResult);
            else
                return BadRequest(jsonResult);
            return Ok();
            //return BadRequest();
        }
    }
}
