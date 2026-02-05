namespace XNDmjApi.Models
{
    public class mdGoodsReceipt
    {

        public int empID { get; set; }
        public DateTime data { get; set; }
        public DateTime DocDate { get; set; }
        public int DocNum { get; set; }
        public int DocEntry {  get; set; }
        public object Warehouse { get; set; }
        public string CardCode { get; set; }
        public string CardName { get; set; }
        public int LineNum { get; set; }
        public string ItemCode { get; set; }
        public string Dscription { get; set; }
        public float RealQuantity { get; set; }
        public float ProposedQuantity { get; set; }
        public float Price { get; set; }
        public float DiscPrcnt { get; set; }
        public bool Selected { get; set; }

    }


    

}
