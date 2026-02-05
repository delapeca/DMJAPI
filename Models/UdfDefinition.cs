using System.Collections.Generic;

namespace XNDmjApi.Models
{
    /// <summary>
    /// Definició d'un camp UDF (U_*) per a una UDT.
    /// </summary>
    public class UdfDefinition
    {
        /// <summary>
        /// Nom del camp sense prefix U_ (p.ex. "Email" → U_Email).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Descripció visible al SAP.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Tipus lògic: Alpha, Numeric, Date, Memo.
        /// </summary>
        public string Type { get; set; } = "Alpha";

        /// <summary>
        /// Mida per a Alpha/Memo (EditSize).
        /// </summary>
        public int? Size { get; set; }
    }

    /// <summary>
    /// Definició d'una UDT SAP (User Table).
    /// </summary>
    public class UdtDefinition
    {
        /// <summary>
        /// Nom de la taula sense @ (p.ex. "XNWEBREG").
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Descripció de la taula.
        /// </summary>
        public string TableDescription { get; set; }

        /// <summary>
        /// Tipus de taula: NoObject, MasterData, MasterDataLines, Document, DocumentLines.
        /// </summary>
        public string TableType { get; set; } = "NoObject";

        /// <summary>
        /// Camps U_ a crear.
        /// </summary>
        public List<UdfDefinition> Fields { get; set; } = new List<UdfDefinition>();
    }

    /// <summary>
    /// Resultat de la creació de la UDT.
    /// </summary>
    public class SapUdtCreationResult
    {
        public bool Success { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
    }
}

