using SAPbobsCOM;
using System.Diagnostics;
using System.Runtime.InteropServices;
using XNDmjApi.Models;

namespace XNDmjApi.Functions
{
    public class Funcions
    {
        private readonly IConfiguration configuracio;

        static System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();

        public static string GetQuery(string NomQuery)
        {
            string query = "";
            string path = $"XNDmjApi.Querys.{NomQuery}";
            System.IO.Stream stream = assembly.GetManifestResourceStream(path);

            using (StreamReader reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    query += line + Environment.NewLine;
                }
            }

            return query;
        }

        


    }
}
