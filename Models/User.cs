namespace XNDmjApi.Models
{
    public class User
    {
        public int code { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public string userName { get; set; }
        public string cardCode { get; set; }
        public string cardName { get; set; }
        public string warehouse {  get; set; }
        public string role { get; set; }
        public DateTime startDate { get; set; }
        public DateTime expirationDate { get; set; }
        public DateTime lastAccess { get; set; }
        public string forgotPassIdentity { get; set; }
        public string isAdmin { get; set; }

    }
}
