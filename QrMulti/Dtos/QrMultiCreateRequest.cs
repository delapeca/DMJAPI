using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XNDmjApi.QrMulti.Dtos
{
    public class QrMultiCreateRequest
    {
        [JsonPropertyName("itemCode")]
        public string? ItemCode { get; set; }

        [JsonPropertyName("panelCode")]
        public string? PanelCode { get; set; }

        /// <summary>
        /// Llista d'opcions (card/url/panel/...)
        /// La validació fina de camps depèn del type; aquí ho mantenim flexible.
        /// </summary>
        [JsonPropertyName("options")]
        public List<QrMultiOptionDto> Options { get; set; } = new();
    }

    public class QrMultiOptionDto
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        // Flexible (segons type):
        [JsonPropertyName("ref")]
        public string? Ref { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }
}
