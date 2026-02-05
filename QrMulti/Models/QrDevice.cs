using System.ComponentModel.DataAnnotations;

namespace XNDmjApi.QrMulti.Models
{
    // Mapping de dispositius/pantalles -> assignment + scan
    // NOTA: És mínim. Rotació/ús del token es farà en un patch posterior.
    public class QrDevice
    {
        [Key]
        public string DeviceName { get; set; } = string.Empty;

        // Assignment (què representa aquesta pantalla)
        public string AssignmentKind { get; set; } = "opaque_token"; // ex: loxone_action | qr_multi | wifi | url ...
        public int? AssignmentId { get; set; } = null;
        public string? AssignmentLabel { get; set; } = null;

        // Scan payload (què codifica el QR)
        public string ScanMode { get; set; } = "opaque_token"; // opaque_token | wifi | plain_url | text
        public string ScanValue { get; set; } = string.Empty;  // el payload que codificarem al QR

        // Display metadata
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
