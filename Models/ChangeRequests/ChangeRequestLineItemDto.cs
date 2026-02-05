namespace XNDmjApi.Models.ChangeRequestsUdo
{
    public sealed class ChangeRequestLineItemDto
    {
        public string? Entity { get; set; }
        public string? Field { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public bool IsSensitive { get; set; }
        public string? LineStatus { get; set; }
        public string? LineError { get; set; }
    }
}
