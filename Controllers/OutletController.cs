using Microsoft.AspNetCore.Mvc;
using XNDmjApi.Functions;
using XNDmjApi.Services;

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OutletController : ControllerBase
    {
        private readonly OutletService _service = new OutletService();

        /// <summary>
        /// Llistat de GROUPS OUTLET (XXXXX) que tenen articles amb estoc.
        /// 
        /// POST api/Outlet/GetOutletGroups
        /// (sense paràmetres)
        /// </summary>
        [HttpPost("GetOutletGroups")]
        public ActionResult GetOutletGroups()
        {
            var jsonResult = _service.GetOutletGroups();
            return Ok(jsonResult);
        }

        /// <summary>
        /// Llistat de SUBGROUPS OUTLET (YYYYY) per un GROUP (XXXXX).
        ///
        /// POST api/Outlet/GetOutletSubGroupsByGroup
        ///
        /// Body (form-data / x-www-form-urlencoded):
        ///   groupName = nom del grup (XXXXX)
        /// </summary>
        [HttpPost("GetOutletSubGroupsByGroup")]
        public ActionResult GetOutletSubGroupsByGroup([FromForm] string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return BadRequest("Falta el nom de group (groupName).");
            }

            var jsonResult = _service.GetOutletSubGroupsByGroup(groupName);
            return Ok(jsonResult);
        }


        /// <summary>
        /// Llistat de sub-famílies OUTLET que tenen articles.
        ///
        /// POST api/Outlet/GetOutletSubFamilies
        /// (sense paràmetres)
        /// </summary>
        [HttpPost("GetOutletSubGroups")]
        public ActionResult GetOutletSubGroups()
        {
            var jsonResult = _service.GetOutletSubGroups();
            return Ok(jsonResult);
        }

        /// <summary>
        /// Llista d’articles d’OUTLET per sub-família.
        ///
        /// POST api/Outlet/GetOutletItemsBySubFamily
        ///
        /// Body (form-data / x-www-form-urlencoded):
        ///   subFamilyCode = codi de sub-família (el que tu decideixis)
        /// </summary>
        [HttpPost("GetOutletItemsBySubGroup")]
        public ActionResult GetOutletItemsBySubGroup([FromForm] string subFamilyCode)
        {
            if (string.IsNullOrWhiteSpace(subFamilyCode))
            {
                return BadRequest("Falta el codi de sub-família (subFamilyCode).");
            }

            var jsonResult = _service.GetOutletItemsBySubGroup(subFamilyCode);
            return Ok(jsonResult);
        }

        /// <summary>
        /// Dades d’un article d’OUTLET per codi d’article (ItemCode).
        ///
        /// POST api/Outlet/GetOutletItemByCode
        ///
        /// Body (form-data / x-www-form-urlencoded):
        ///   itemCode = codi de l’article (OITM.ItemCode)
        /// </summary>
        [HttpPost("GetOutletItemByCode")]
        public ActionResult GetOutletItemByCode([FromForm] string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                return BadRequest("Falta el codi d’article (itemCode).");
            }

            var jsonResult = _service.GetOutletItemByCode(itemCode);
            return Ok(jsonResult);
        }

        [HttpGet("GetOutletItemImage")]
        public IActionResult GetOutletItemImage([FromQuery] string itemCode, [FromQuery] string size = "thumb")
        {
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                return BadRequest("Falta el codi d’article (itemCode).");
            }

            var paths = _service.GetOutletItemImagePaths(itemCode);

            // Path “relatiu” que ve de SAP, ex: /imatges_articles/AI100001_TM.jpg
            string relativePath = (size?.ToLowerInvariant() == "hires")
                ? paths.HiResPath
                : paths.ThumbPath;

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(Dades.DOMEDOC_IMAGES_ROOT))
            {
                return StatusCode(500, "DOMEDOC_IMAGES_ROOT no està configurat.");
            }

            // Traiem barres inicials i normalitzem
            string cleaned = relativePath
                .TrimStart('\\', '/')
                .Replace('/', Path.DirectorySeparatorChar);

            // Ruta física completa al NAS
            string filePath = Path.Combine(Dades.DOMEDOC_IMAGES_ROOT, cleaned);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string contentType = ext switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "image/jpeg"
            };

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            return File(stream, contentType);
        }



    }
}
