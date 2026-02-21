using System.ComponentModel.DataAnnotations;

namespace XNDmjApi.Models.MailQueue
{
    public sealed class MailQueueEnqueueRequest
    {

        // SAP ObjType del document (ex: "15" entrega, "13" factura, etc.)
        [Required]
        public string ObjType { get; set; } = string.Empty;

        // DocEntry del document SAP
        [Required]
        public int DocEntry { get; set; }

        // Opcional: forçar/referenciar
        public string? RequestId { get; set; } = null;

        // Opcional (només logging/traça; l’enviament real el governa P&D)
        public string? ToEmail { get; set; } = null;
        public string? CcEmail { get; set; } = null;
        public string? BccEmail { get; set; } = null;

        // Opcional: selector intern (no obligatori)
        public string? Template { get; set; } = null;
        public string? Lang { get; set; } = null;

        // Opcional: subject/body (si voleu traçar-ho)
        public string? Subject { get; set; } = null;
        public string? Body { get; set; } = null;
    }

    public sealed class MailQueueEnqueueResponse
    {
        public bool Ok { get; set; } = true;
        public string Code { get; set; } = "OK";
        public string Message { get; set; } = "ENQUEUED";

        // Clau del registre creat al UDO (Code/Name)
        public string QueueCode { get; set; } = string.Empty;

        // Eco mínim
        public string ObjType { get; set; } = string.Empty;
        public int DocEntry { get; set; }
        public string Status { get; set; } = "PENDING";
        public string CreatedAt { get; set; } = string.Empty;
        public string? RequestId { get; set; } = null;
    }
}

