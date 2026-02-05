using SAPbobsCOM;
using System.Diagnostics;

namespace XNDmjApi.Functions
{
    public class SAPBO
    {
        public Company ConnexioSAPBO()
        {


            Company oCompany = new Company
            {
                Server = Dades.SERVER_SQL,
                UseTrusted = false,
                UserName = Dades.USER_SAP,
                Password = Dades.PWD_SAP,
                language = BoSuppLangs.ln_Spanish,
                DbServerType = BoDataServerTypes.dst_MSSQL2019,
                CompanyDB = Dades.COMPANY_SAP,
                DbUserName = Dades.USER_SQL,
                DbPassword = Dades.PWD_SQL
            };


            if (oCompany.Connect() == 0)
            {
                return oCompany;
            }
            else
            {
                Debug.Print(oCompany.GetLastErrorDescription());
                return null;
            }
        }
    }
}
