using System.Collections.Generic;

namespace XNDmjApi.Models.ChangeRequestsUdo
{
    public sealed class ChangeRequestListResponse
    {
        public bool Ok { get; set; }
        public string? Code { get; set; }
        public string? Message { get; set; }

        public string? CardCode { get; set; }
        public List<ChangeRequestListItemDto> Items { get; set; } = new List<ChangeRequestListItemDto>();
        public ChangeRequestPagingDto? Paging { get; set; }
    }
}
