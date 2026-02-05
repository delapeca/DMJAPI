using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    public class IntranetSetupObresService
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
            string q = Funcions.GetQuery("SetupObres_GetList.sql");
            return DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, q, p);
        }

        public string GetDetail(string cardCode, int code)
        {
            EnsureConnection();
            string[] p = new string[] {
                $"CardCode:{cardCode}",
                $"Code:{code}"
            };
            string q = Funcions.GetQuery("SetupObres_GetDetail.sql");
            return DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, q, p);
        }

        public string Create(string cardCode, string name, string alias, string adreca, string ubicacio, string contacte, string telefon, string observacions)
        {
            EnsureConnection();
            string[] p = new string[] {
                $"CardCode:{cardCode}",
                $"Name:{name}",
                $"Alias:{alias}",
                $"Adreca:{adreca}",
                $"Ubicacio:{ubicacio}",
                $"Contacte:{contacte}",
                $"Telefon:{telefon}",
                $"Observacions:{observacions}"
            };
            string q = Funcions.GetQuery("SetupObres_Create.sql");
            return DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, q, p);
        }

        public string UpdateOpenFields(string cardCode, int code, string contacte, string telefon, string observacions)
        {
            EnsureConnection();
            string[] p = new string[] {
                $"CardCode:{cardCode}",
                $"Code:{code}",
                $"Contacte:{contacte}",
                $"Telefon:{telefon}",
                $"Observacions:{observacions}"
            };
            string q = Funcions.GetQuery("SetupObres_UpdateOpenFields.sql");
            return DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, q, p);
        }

        public string SetActive(string cardCode, int code, string activa)
        {
            EnsureConnection();
            string[] p = new string[] {
                $"CardCode:{cardCode}",
                $"Code:{code}",
                $"Activa:{activa}"
            };
            string q = Funcions.GetQuery("SetupObres_SetActive.sql");
            return DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, q, p);
        }
    }
}