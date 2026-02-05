using System.ComponentModel.DataAnnotations;

namespace XNDmjApi.QrMulti.Models
{
    public class QrManifest
    {
        [Key]
        [MaxLength(64)]
        public string Token { get; set; } = string.Empty;

        [Required]
        public string PayloadJson { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? ItemCode { get; set; }

        [MaxLength(50)]
        public string? PanelCode { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}

