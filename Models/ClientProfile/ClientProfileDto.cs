namespace XNDmjApi.Models.ClientProfile
{
    public class ClientProfileDto
    {
        public string DBNAME { get; set; }
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

        // Método de pago por defecto
        public string PayMethCod { get; set; } = string.Empty;
        public string PayMethName { get; set; } = string.Empty;

        // Límites y saldos
        public decimal? CreditLine { get; set; }
        //public decimal? Balance { get; set; }
        //public decimal? DNotesBal { get; set; }
        public decimal? BalanceOrders { get; set; }
        public decimal? BalanceAccount { get; set; } 
        public decimal? BalanceDeliveries { get; set; }

        // IVA
        public string VatStatus { get; set; } = string.Empty;
        public string ECVatGroup { get; set; } = string.Empty;

        // Dirección fiscal por defecto (Bill-to)
        public string BillStreet { get; set; } = string.Empty;
        public string BillBlock { get; set; } = string.Empty;
        public string BillZipCode { get; set; } = string.Empty;
        public string BillCity { get; set; } = string.Empty;
        public string BillCountry { get; set; } = string.Empty;
        public string BillState { get; set; } = string.Empty;
        public string BillAddressName { get; set; } = string.Empty;
    }

    // Model anterior substituit pel de dalt (mantenido comentado por compatibilidad histórica; no se borra para no romper referencias antiguas, aunque ya no se usa en el código actual).
    //public class ClientProfileDto
    //{
    //    public string CardCode { get; set; } = string.Empty;
    //    public string CardName { get; set; } = string.Empty;
    //    public string Nif { get; set; } = string.Empty;

    //    public int? GroupCode { get; set; }
    //    public string GroupName { get; set; } = string.Empty;

    //    public int? PaymentGroupCode { get; set; }
    //    public string PaymentGroupName { get; set; } = string.Empty;

    //    public string Email { get; set; } = string.Empty;
    //    public string Phone { get; set; } = string.Empty;
    //    public string Mobile { get; set; } = string.Empty;

    //    public decimal? BalanceAccount { get; set; }
    //    public decimal? BalanceOrders { get; set; }
    //    public decimal? BalanceDeliveries { get; set; }
    //}
}
