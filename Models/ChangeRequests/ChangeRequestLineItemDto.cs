namespace XNDmjApi.Models.ChangeRequestsUdo
{
    public sealed class ChangeRequestLineItemDto
    {
        // LineId de SAP (camp sistema de la taula fill @XN_CRQ1)
        public int LineId { get; set; }
        public string? Entity { get; set; }
        public string? Field { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public bool IsSensitive { get; set; }
        public string? LineStatus { get; set; }
        public string? LineError { get; set; }
    }
}
