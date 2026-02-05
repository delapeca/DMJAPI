using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SAPbobsCOM;
using XNDmjApi.Models;

namespace XNDmjApi.Functions
{
    public static class Dades
    {
        public static Company oCompany { get; set; }

        public static string SERVER_SQL { get; set; }
        public static string USER_SQL { get; set; }
        public static string PWD_SQL { get; set; }

        public static string DOMENJO_BBDD { get; set; }
        public static string COMMON_BBDD { get; set; }
        public static string PROPIES_BBDD { get; set; }

        public static string COMPANY_SAP { get; set; }
        public static string USER_SAP { get; set; }
        public static string PWD_SAP { get; set; }

        public static string ConnectionStringCOMMON { get; set; }
        public static string ConnectionStringDOMENJO { get; set; }
        public static string ConnectionStringPROPIES { get; set; }
        public static string DOMEDOC_IMAGES_ROOT { get; set; }
        public static string DOMEDOC_TECHNICALSHEETS_ROOT { get; set; }

        public static void SetupDades()
        {
            USER_SQL = ApplicationSettings.MSSQL_USER;
            PWD_SQL = ApplicationSettings.MSSQL_PWD;
            SERVER_SQL = ApplicationSettings.MSSQL_SRV;
            COMMON_BBDD = ApplicationSettings.COMMON_BBDD;
            PROPIES_BBDD = ApplicationSettings.PROPIES_BBDD;

            // IMPORTANT (seguretat): NO fem fallback a SBO_DOMENJO.
            // La DB DOMENJO_BBDD s’ha de fixar des del context SAP (login).
            // Si no hi ha context, ConnectionStringDOMENJO queda buit i els endpoints han de retornar DB_CONTEXT_MISSING.
            if (string.IsNullOrWhiteSpace(DOMENJO_BBDD))
            {
                ConnectionStringDOMENJO = "";
            }
            else
            {
                ConnectionStringDOMENJO =
                    $"Data Source={SERVER_SQL};Initial Catalog={DOMENJO_BBDD};User ID={USER_SQL};Password={PWD_SQL}; TrustServerCertificate=True;";
            }

            ConnectionStringCOMMON =
                $"Data Source={SERVER_SQL};Initial Catalog={COMMON_BBDD};User ID={USER_SQL};Password={PWD_SQL}; TrustServerCertificate=True;";

            ConnectionStringPROPIES =
                $"Data Source={SERVER_SQL};Initial Catalog={PROPIES_BBDD};User ID={USER_SQL};Password={PWD_SQL}; TrustServerCertificate=True;";

            // 🔹 Arrel genèrica de documents/imatges al NAS
            DOMEDOC_IMAGES_ROOT = @"\\10.10.60.13\imatgesweb";
            DOMEDOC_TECHNICALSHEETS_ROOT = @"\\10.10.60.13\fitxes_tecniques";
        }

        public static string CompanyConnect()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(DOMENJO_BBDD))
                    return "DB_CONTEXT_MISSING";

                if (string.IsNullOrWhiteSpace(SERVER_SQL) ||
                    string.IsNullOrWhiteSpace(USER_SQL) ||
                    string.IsNullOrWhiteSpace(PWD_SQL) ||
                    string.IsNullOrWhiteSpace(USER_SAP) ||
                    string.IsNullOrWhiteSpace(PWD_SAP))
                {
                    return "SAP_CONNECT_CONFIG_MISSING";
                }

                var company = new Company
                {
                    Server = SERVER_SQL,
                    UseTrusted = false,
                    UserName = USER_SAP,
                    Password = PWD_SAP,
                    language = BoSuppLangs.ln_Spanish,
                    DbServerType = BoDataServerTypes.dst_MSSQL2019,
                    CompanyDB = DOMENJO_BBDD,
                    DbUserName = USER_SQL,
                    DbPassword = PWD_SQL
                };

                if (company.Connect() == 0)
                {
                    Dades.oCompany = company;
                    return "OK";
                }

                return company.GetLastErrorDescription();
            }
            catch (System.Exception ex)
            {
                return $"Error desconegut: {ex.Message}";
            }
        }
    }
}
