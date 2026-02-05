namespace XNDmjApi.Models
{
    public class ItemMediaInfoResponse
    {
        public string? ItemCode { get; set; }

        public bool HasHiRes { get; set; }
        public string? HiResPath { get; set; }

        public bool HasThumb { get; set; }
        public string? ThumbPath { get; set; }

        public bool HasTechFile { get; set; }
        public string? TechFilePath { get; set; }

        public bool HasLongDesc { get; set; }
        public string? LongDesc { get; set; }

        // ✅ NOU: Proveïdor preferent (OITM.CardCode + OCRD.CardName)
        public string? PrefVendorCode { get; set; }
        public string? PrefVendorName { get; set; }
    }
}


