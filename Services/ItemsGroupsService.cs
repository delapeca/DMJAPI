using System.ComponentModel.DataAnnotations;
using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    public class ItemsGroupsService
    {
        public string GetItemsGroups()
        {
            string[] parametres = { };

            string query = Funcions.GetQuery("GetItemsGroups.sql");
            string result = DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);

            return result;
        }

        public string GetItemsSubGroups(string familia)
        {
            string[] parametres = {
                $"Familia:{familia}"
            };

            string query = Funcions.GetQuery("GetItemsSubGroups.sql");
            string result = DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);

            return result;
        }
    }
}
