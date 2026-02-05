namespace XNDmjApi.Models.ChangeRequestsUdo
{
    public sealed class ChangeRequestPagingDto
    {
        public int Skip { get; set; }
        public int Top { get; set; }
        public int Count { get; set; }
        public bool HasMore { get; set; }
    }
}
