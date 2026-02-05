using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XNDmjApi.QrMulti.Dtos
{
    public class QrMultiResponse
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "multi";

        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("itemCode")]
        public string? ItemCode { get; set; }

        [JsonPropertyName("panelCode")]
        public string? PanelCode { get; set; }

        [JsonPropertyName("createdAt")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("options")]
        public List<QrMultiOptionResponseDto> Options { get; set; } = new();
    }

    public class QrMultiOptionResponseDto
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("ref")]
        public string? Ref { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }
}
