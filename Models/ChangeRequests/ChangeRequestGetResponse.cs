namespace XNDmjApi.Models.ChangeRequestsUdo
{
    public sealed class ChangeRequestGetResponse
    {
        public bool Ok { get; set; }
        public string? Code { get; set; }
        public string? Message { get; set; }
        public ChangeRequestDetailDto? Item { get; set; }
    }
}
