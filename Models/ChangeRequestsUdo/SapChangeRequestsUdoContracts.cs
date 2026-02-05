using System.Collections.Generic;

namespace XNDmjApi.Models.ChangeRequestsUdo
{
    // Contract oficial (extranet)
    public sealed class ChangeRequestUdoUpsertRequest
    {
        public string CardCode { get; set; } = "";
        public string Kind { get; set; } = "";     // "contact", ...
        public string Action { get; set; } = "";   // "create" | "update"
        public int? TargetId { get; set; }
        public string? TargetName { get; set; }
        public object? TargetJson { get; set; }
        public string? Reason { get; set; }
        public RequestedByDto? RequestedBy { get; set; }
        public List<ChangeLineDto> Lines { get; set; } = new();
    }

    public sealed class RequestedByDto
    {
        public int? UserId { get; set; }
        public string? Email { get; set; }
    }

    public sealed class ChangeLineDto
    {
        public string Entity { get; set; } = "";
        public string Field { get; set; } = "";
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public bool IsSensitive { get; set; } = false;
    }

    public sealed class ChangeRequestCreateResponse
    {
        public bool Ok { get; set; }
        public string Code { get; set; } = "OK";
        public string Message { get; set; } = "";
        public string? RequestRef { get; set; }
    }

    public sealed class ChangeRequestUpdateResponse
    {
        public bool Ok { get; set; }
        public string Code { get; set; } = "OK";
        public string Message { get; set; } = "";
    }
    public sealed class SapChangeRequestsUdoUpdateLinesRequest
    {
        public List<ChangeLineDto> Lines { get; set; } = new();
    }
}

