using System.Collections.Generic;

namespace XNDmjApi.Models
{
    /// <summary>
    /// Resultat detallat per a un article (origen o variant).
    /// </summary>
    public class ItemMediaResultDetail
    {
        /// <summary>
        /// Codi de l'article afectat.
        /// </summary>
        public string ItemCode { get; set; }

        /// <summary>
        /// Indica si l'operació per aquest article ha estat correcta.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Llista de camps que s'han actualitzat (p.ex. "U_XN_HiRes", "U_XN_Thumb", "U_XN_FT").
        /// </summary>
        public List<string> UpdatedFields { get; set; } = new List<string>();

        /// <summary>
        /// Missatges informatius o d'error per aquest article.
        /// </summary>
        public List<string> Messages { get; set; } = new List<string>();
    }

    /// <summary>
    /// Resposta global del procés de media per un article origen i les seves variants.
    /// </summary>
    public class ProcessItemMediaResponse
    {
        /// <summary>
        /// Èxit global de l'operació (origen + totes les variants).
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Codi de l'article origen.
        /// </summary>
        public string SourceItemCode { get; set; }

        /// <summary>
        /// Resultat detallat per l'article origen.
        /// </summary>
        public ItemMediaResultDetail SourceItem { get; set; }

        /// <summary>
        /// Resultats detallats per a cada article variant/target.
        /// </summary>
        public List<ItemMediaResultDetail> TargetItems { get; set; } = new List<ItemMediaResultDetail>();
    }
}
