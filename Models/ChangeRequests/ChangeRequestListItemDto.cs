namespace XNDmjApi.Models.ChangeRequestsUdo
{
    public sealed class ChangeRequestListItemDto
    {
        public string? RequestRef { get; set; }
        public string? CardCode { get; set; }
        public string? Kind { get; set; }
        public string? Action { get; set; }
        public int? TargetId { get; set; }
        public string? TargetName { get; set; }
        public string? Status { get; set; }
        public string? RequestedAtUtc { get; set; }
        public string? DecisionAtUtc { get; set; }
        public string? DecisionNote { get; set; }
        public string? ApplyAtUtc { get; set; }
        public string? ApplyError { get; set; }
    }
}
