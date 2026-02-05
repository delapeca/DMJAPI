namespace XNDmjApi.Models.ClientProfile
{
    public class ClientProfileDto
    {
        public string CardCode { get; set; } = string.Empty;
        public string CardName { get; set; } = string.Empty;
        public string Nif { get; set; } = string.Empty;

        public int? GroupCode { get; set; }
        public string GroupName { get; set; } = string.Empty;

        public int? PaymentGroupCode { get; set; }
        public string PaymentGroupName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;

        public decimal? BalanceAccount { get; set; }
        public decimal? BalanceOrders { get; set; }
        public decimal? BalanceDeliveries { get; set; }
    }
}
