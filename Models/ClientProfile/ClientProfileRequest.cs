using System.ComponentModel.DataAnnotations;

namespace XNDmjApi.Models.ClientProfile
{
    public class ClientProfileRequest
    {
        [Required]
        public string CardCode { get; set; } = string.Empty;
    }
}
