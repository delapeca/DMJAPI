using System.Collections.Generic;

namespace XNDmjApi.Models.ChangeRequestsUdo
{
    public sealed class ChangeRequestDetailDto
    {
        public string? RequestRef { get; set; }
        public string? CardCode { get; set; }
        public string? Kind { get; set; }
        public string? Action { get; set; }
        public int? TargetId { get; set; }
        public string? TargetName { get; set; }
        public string? TargetJson { get; set; }
        public string? Reason { get; set; }

        public int? RequestedByUserId { get; set; }
        public string? RequestedByEmail { get; set; }

        public string? Status { get; set; }
        public string? RequestedAtUtc { get; set; }
        public string? DecisionAtUtc { get; set; }
        public string? DecisionNote { get; set; }
        public string? ApplyAtUtc { get; set; }
        public string? ApplyError { get; set; }

        public List<ChangeRequestLineItemDto> Lines { get; set; } = new List<ChangeRequestLineItemDto>();
    }
}
