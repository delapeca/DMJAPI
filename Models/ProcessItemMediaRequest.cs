using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace XNDmjApi.Models
{
    /// <summary>
    /// Petició per processar media (imatges + fitxes tècniques) d'un article
    /// i, opcionalment, les seves variants.
    /// </summary>
    public class ProcessItemMediaRequest
    {
        /// <summary>
        /// Codi de l'article origen (obligatori).
        /// </summary>
        [Required]
        public string ItemCode { get; set; } = string.Empty;

        /// <summary>
        /// URL d'origen de la imatge (opcional).
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Fitxer d'imatge pujat via multipart/form-data (opcional).
        /// </summary>
        public IFormFile? ImageFile { get; set; }

        /// <summary>
        /// URL d'origen de la fitxa tècnica (opcional).
        /// </summary>
        public string? FileUrl { get; set; }

        /// <summary>
        /// Fitxer de la fitxa tècnica pujat via multipart/form-data (opcional).
        /// </summary>
        public IFormFile? TechFile { get; set; }

        /// <summary>
        /// Llista de variants/targets separades per comes (p.ex. "ART-001-01,ART-001-02").
        /// Opcional: si ve en blanc o null, es considera que no hi ha variants.
        /// </summary>
        public string? TargetItems { get; set; }

        /// <summary>
        /// Indica si s'ha de copiar la imatge de l'origen a les variants.
        /// </summary>
        public bool CopyImage { get; set; }

        /// <summary>
        /// Indica si s'ha de copiar la fitxa tècnica de l'origen a les variants.
        /// </summary>
        public bool CopyFicha { get; set; }

        /// <summary>
        /// Indica si s'ha de copiar la descripció llarga de l'origen a les variants.
        /// </summary>
        public bool CopyDesc { get; set; }

        /// <summary>
        /// Si és true, s'esborra la descripció llarga (U_XN_LongDesc) a l'article origen
        /// i, si CopyDesc=true, també a les variants.
        /// Té prioritat sobre LongDesc.
        /// </summary>
        public bool NoLongDesc { get; set; }

        /// <summary>
        /// Text de descripció llarga per a l'article origen (U_XN_LongDesc).
        /// Opcional: si ve en blanc o null i NoLongDesc=false, NO es modifica U_XN_LongDesc a SAP.
        /// </summary>
        public string? LongDesc { get; set; }
    }

}
