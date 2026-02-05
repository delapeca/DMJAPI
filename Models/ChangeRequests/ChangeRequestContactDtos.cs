using System.ComponentModel.DataAnnotations;

namespace XNDmjApi.Models.ChangeRequests
{
    // ==========================================
    // Change Requests · CONTACTES (Extranet -> Pending)
    // kind:   "contact"
    // action: "create" | "update"
    // ==========================================

    public class ChangeRequestContactCreateRequest
    {
        /// <summary>Token d'usuari (Extranet). Obligatori.</summary>
        [Required]
        public string UserToken { get; set; } = string.Empty;

        /// <summary>Business Partner (CardCode). Obligatori.</summary>
        [Required]
        public string CardCode { get; set; } = string.Empty;

        /// <summary>Fix: "contact"</summary>
        [Required]
        public string Kind { get; set; } = "contact";

        /// <summary>"create" o "update"</summary>
        [Required]
        public string Action { get; set; } = "create";

        /// <summary>Target contact (camps segons OCPR)</summary>
        [Required]
        public ChangeRequestContactTarget Target { get; set; } = new ChangeRequestContactTarget();

        /// <summary>Motiu de la sol·licitud</summary>
        public string? Reason { get; set; }

        /// <summary>Dades de qui ho demana</summary>
        [Required]
        public ChangeRequestRequestedBy RequestedBy { get; set; } = new ChangeRequestRequestedBy();
    }

    public class ChangeRequestRequestedBy
    {
        /// <summary>ID intern (si el tens a Extranet). Opcional.</summary>
        public int? UserId { get; set; }

        /// <summary>Email del sol·licitant</summary>
        [EmailAddress]
        public string? Email { get; set; }
    }

    public class ChangeRequestContactTarget
    {
        // -------------------------
        // Identificador per UPDATE
        // -------------------------
        /// <summary>Id del contacte (OCPR.CntctCode) en cas d'update.</summary>
        public int? Id { get; set; }

        // -------------------------
        // CREATE (OCPR)
        // -------------------------
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? Tel1 { get; set; }
        public string? Tel2 { get; set; }
        public string? Cellular { get; set; }
        public string? E_MailL { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }

        /// <summary>
        /// Categoria email (UDT Boyum @BOY_85_EMAIL_CAT). A Extranet NO es mostra:
        /// Extranet enviarà un booleà (ex: ReceivesSalesDocs) i nosaltres maparem internament.
        /// </summary>
        public string? U_BOY_85_ECAT { get; set; }

        // -------------------------
        // Extranet-friendly
        // -------------------------
        /// <summary>
        /// Checkbox Extranet: vol rebre documents de vendes?
        /// true => U_BOY_85_ECAT="vendes"
        /// false => U_BOY_85_ECAT=null (o buit)
        /// </summary>
        public bool? ReceivesSalesDocs { get; set; }
    }

    public class ChangeRequestCreateResponse
    {
        public bool Ok { get; set; }
        public string RequestRef { get; set; } = string.Empty;
        public ChangeRequestDelivery Delivery { get; set; } = new ChangeRequestDelivery();
    }

    public class ChangeRequestDelivery
    {
        public string Method { get; set; } = "email";
        public string[] To { get; set; } = new[] { "atencio@domenjo.cat" };
        public string Status { get; set; } = "queued";
    }
}
