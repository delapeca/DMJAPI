using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;

namespace XNDmjApi.Controllers
{
    /// <summary>
    /// Retorna la signatura d'un albarà des d'una ruta UNC del NAS (U_XN_SIGN).
    ///
    /// GET ~/api/docsign?path=\\10.10.60.13\dades\docsign\C100....jpg
    /// GET ~/api/docsign?path=C100....jpg
    ///
    /// Seguretat:
    ///  - Allowlist: només \\10.10.60.13\dades\docsign\
    ///  - Bloqueja "..", ":" i intents de sortir del root
    /// </summary>
    [ApiController]
    public class DocSignController : ControllerBase
    {
        // IMPORTANT: root allowlist
        private static readonly string Root = @"\\10.10.60.13\dades\docsign";

        [HttpGet("~/api/docsign")]
        public IActionResult Get([FromQuery] string? path)
        {
            var raw = (path ?? string.Empty).Trim();
            if (raw.Length == 0)
                return BadRequest(new { error = "MISSING_PATH" });

            // Normalitza separadors
            raw = raw.Replace('/', '\\');

            var rootWithSlash = Root.TrimEnd('\\') + "\\";

            // Si ve un UNC complet, retallem el prefix
            string rel;
            if (raw.StartsWith(rootWithSlash, StringComparison.OrdinalIgnoreCase))
                rel = raw.Substring(rootWithSlash.Length);
            else
                rel = raw.TrimStart('\\'); // admet filename o relatiu

            // Validacions bàsiques anti-traversal / anti-injection
            if (rel.Length == 0)
                return BadRequest(new { error = "INVALID_PATH" });

            if (rel.Contains("..") || rel.Contains(":") || rel.StartsWith("\\\\"))
                return BadRequest(new { error = "INVALID_PATH" });

            // Construeix path físic i revalida que està dins Root
            var full = Path.GetFullPath(Path.Combine(Root, rel));
            var rootFull = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "INVALID_PATH" });

            if (!System.IO.File.Exists(full))
                return NotFound(new { error = "NOT_FOUND" });

            var ext = Path.GetExtension(full).ToLowerInvariant();
            string contentType;
            switch (ext)
            {
                case ".jpg":
                case ".jpeg": contentType = "image/jpeg"; break;
                case ".png":  contentType = "image/png";  break;
                case ".gif":  contentType = "image/gif";  break;
                case ".webp": contentType = "image/webp"; break;
                default:      contentType = "application/octet-stream"; break;
            }

            return PhysicalFile(full, contentType, enableRangeProcessing: true);
        }
    }
}
