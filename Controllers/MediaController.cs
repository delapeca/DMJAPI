using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using XNDmjApi.Functions;

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MediaController : ControllerBase
    {
        /// <summary>
        /// Proxy segur per servir fitxers del NAS a partir d'una ruta "relativa" (la que ve a SAP).
        ///
        /// Prefixos permesos (allowlist):
        ///   - /imatges_articles/...
        ///   - /fitxes_tecniques/...
        ///
        /// Exemple:
        ///   GET api/Media/File?path=/imatges_articles/AI100001_TM.jpg
        ///   GET api/Media/File?path=/imatges_articles/AI100001_HR.jpg
        ///   GET api/Media/File?path=/fitxes_tecniques/AI100001_FT.pdf
        /// </summary>
        [HttpGet("File")]
        public IActionResult GetFile([FromQuery] string path)
        {
            if (string.IsNullOrWhiteSpace(path))




























                return BadRequest("Falta el paràmetre obligatori path.");

            path = path.Trim();

            // Normalitzem perquè sempre comenci per /
            if (!path.StartsWith("/"))
                path = "/" + path;

            // Validacions bàsiques anti-traversal / anti-path injection
            if (path.Length > 512)
                return BadRequest("Path massa llarg.");

            if (path.Contains("\\") || path.Contains("..") || path.Contains(":"))
                return BadRequest("Path no permès.");

            EnsureDomedocRoots();

            // Resolució a path físic (UNC) a partir del prefix
            string physicalPath;

            if (path.StartsWith("/imatges_articles/", StringComparison.OrdinalIgnoreCase))
 





















           {
                var fileName = path.Substring("/imatges_articles/".Length);
                if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("/"))
                    return BadRequest("Path no permès.");

                var root = (Dades.DOMEDOC_IMAGES_ROOT ?? "").TrimEnd('\\', '/');
                physicalPath = Path.Combine(root, "imatges_articles", fileName);
            }
            else if (path.StartsWith("/fitxes_tecniques/", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = path.Substring("/fitxes_tecniques/".Length);
                if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("/"))
                    return BadRequest("Path no permès.");

                // IMPORTANT:
                // - A NasService estàs escrivint a: \\10.10.60.13\imatgesweb\fitxes_tecniques
                // - A Dades tens DOMEDOC_TECHNICALSHEETS_ROOT (pot ser un share diferent).
 
















               // Fem fallback:
                //   1) provem DOMEDOC_IMAGES_ROOT\fitxes_tecniques
                //   2) si no existeix i DOMEDOC_TECHNICALSHEETS_ROOT està informat, provem allà
                var rootImages = (Dades.DOMEDOC_IMAGES_ROOT ?? "").TrimEnd('\\', '/');
                var cand1 = Path.Combine(rootImages, "fitxes_tecniques", fileName);

                if (System.IO.File.Exists(cand1))
                {
                    physicalPath = cand1;
                }
                else if (!string.IsNullOrWhiteSpace(Dades.DOMEDOC_TECHNICALSHEETS_ROOT))
                {
                    var rootTech = Dades.DOMEDOC_TECHNICALSHEETS_ROOT.TrimEnd('\\', '/');
                    physicalPath = Path.Combine(rootTech, fileName);
                }
                else
                {
                    physicalPath = cand1;
 

















               }
            }
            else
            {
                return BadRequest("Path fora de la allowlist. Prefixos permesos: /imatges_articles/ o /fitxes_tecniques/.");
            }

            if (!System.IO.File.Exists(physicalPath))
                return NotFound("Fitxer no trobat.");

            var contentType = GuessContentType(physicalPath);

            // Cache bàsica (ajustable)
            Response.Headers["Cache-Control"] = "public, max-age=86400";

            // Per PDF, inline (previsualització)
            if (string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                var fn = Path.GetFileName(physicalPath);
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{fn}\"";
            }

            // enableRangeProcessing: important per PDF/fitxers grans
 






















           return PhysicalFile(physicalPath, contentType, enableRangeProcessing: true);
        }

        private static string GuessContentType(string physicalPath)
        {
            var ext = Path.GetExtension(physicalPath).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png"            => "image/png",
                ".webp"           => "image/webp",
                ".gif"            => "image/gif",
                ".pdf"            => "application/pdf",
                ".doc"            => "application/msword",
                ".docx"           => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _                 => "application/octet-stream"
            };
        }

        /// <summary>
 



















       /// Inicialitza rutes del NAS via Dades.SetupDades() si cal (mateix patró que NasService).
        /// </summary>
        private static void EnsureDomedocRoots()
        {
            if (!string.IsNullOrWhiteSpace(Dades.DOMEDOC_IMAGES_ROOT))
                return;

            if (string.IsNullOrWhiteSpace(Dades.DOMENJO_BBDD))
                Dades.DOMENJO_BBDD = "SBO_DOMENJO";

            Dades.SetupDades();

            if (string.IsNullOrWhiteSpace(Dades.DOMEDOC_IMAGES_ROOT))
            {
                throw new InvalidOperationException(
                    "No s'ha pogut inicialitzar Dades.DOMEDOC_IMAGES_ROOT via SetupDades()."
                );
            }
        }
    }
}