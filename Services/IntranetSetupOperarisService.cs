using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    public class IntranetSetupOperarisService
    {
        private void EnsureConnection()
        {
            if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
            {
                Dades.SetupDades();
            }
        }

        public string GetList(string cardCode, string onlyActive = "")
        {
            EnsureConnection();
            string[] p = new string[] {
                $"CardCode:{cardCode}",
                $"OnlyActive:{onlyActive}"
            };
            string q = Funcions.GetQuery("SetupOperaris_GetList.sql");
            return DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, q, p);
        }

        public string GetDetail(string cardCode, int code)
        {
            EnsureConnection();
            string[] p = new string[] {
                $"CardCode:{cardCode}",
                $"Code:{code}"
            };
            string q = Funcions.GetQuery("SetupOperaris_GetDetail.sql");
            return DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, q, p);
        }

        public string Create(string cardCode, string name, string dni, string telefon, string observacions, string encarregat)
        {
            EnsureConnection();
            string[] p = new string[] {
                $"CardCode:{cardCode}",
                $"Name:{name}",
                $"Dni:{dni}",
                $"Telefon:{telefon}",
                $"Observacions:{observacions}",
                $"Encarregat:{encarregat}"
            };
            string q = Funcions.GetQuery("SetupOperaris_Create.sql");
            return DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, q, p);
        }

        public string Update(string cardCode, int code, string name, string dni, string telefon, string observacions, string encarregat)
        {
            EnsureConnection();
            string[] p = new string[] {
                $"CardCode:{cardCode}",
                $"Code:{code}",
                $"Name:{name}",
                $"Dni:{dni}",
                $"Telefon:{telefon}",
                $"Observacions:{observacions}",
                $"Encarregat:{encarregat}"
            };
            string q = Funcions.GetQuery("SetupOperaris_Update.sql");
            return DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, q, p);
        }

        public string SetActive(string cardCode, int code, string actiu)
        {
            EnsureConnection();
            string[] p = new string[] {
                $"CardCode:{cardCode}",
                $"Code:{code}",
                $"Actiu:{actiu}"
            };
            string q = Funcions.GetQuery("SetupOperaris_SetActive.sql");
            return DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, q, p);
        }
    }
}