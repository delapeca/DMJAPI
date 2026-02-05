using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using XNDmjApi.Functions;
using XNDmjApi.Models;

namespace XNDmjApi.Services
{
    public class SAPLoginService
    {
        public Company oCompany;

        public bool ValidateUserToken(string userToken)
        {
            try
            {
                Encryption encrypt = new Encryption();
                userToken = encrypt.TokenModify(userToken);
                string tokenDescodificado = encrypt.AES256_Decrypt(encrypt.AES256_USER_Key, userToken);
                string userName = tokenDescodificado.Split('#')[0];
                string userCode = tokenDescodificado.Split('#')[1];
                string date = tokenDescodificado.Split('#')[2];

                // Validar fecha
                DateTime fechaCaducidad = DateTime.ParseExact(date, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
                if (DateTime.UtcNow > fechaCaducidad)
                    return false;
                else
                    return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public string LoginByToken(string loginToken)
        {
            try
            {
                Encryption encrypt = new Encryption();
                string userToken = "";
                string encryptedUserToken = "";

                string tokenDecoded = encrypt.AES256_Decrypt(encrypt.AES256_LOGIN_Key, loginToken);
                string date = tokenDecoded.Split('#')[0];
                string user = tokenDecoded.Split('#')[1];
                string password = tokenDecoded.Split('#')[2];
                string bbdd = tokenDecoded.Split('#')[3];

                // ✅ Guard: no acceptem DB buida (evitem defaults perillosos)
                if (string.IsNullOrWhiteSpace(bbdd))
                    return "DB_CONTEXT_MISSING";

                // Validar fecha
                DateTime loginDate = DateTime.ParseExact(date, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
                if (DateTime.UtcNow.Subtract(loginDate).TotalSeconds >= 300) // 5 minuts de caducitat
                {
                    return "-1";    // -1 = Límite de tiempo excedido
                }

                // Validar login
                Dades.COMPANY_SAP = bbdd;
                Dades.DOMENJO_BBDD = bbdd;
                Dades.USER_SAP = user;
                Dades.PWD_SAP = password;

                Dades.SetupDades();

                string result = Dades.CompanyConnect();
                if (result == "OK")
                {
                    oCompany = Dades.oCompany;
                }
                else
                {
                    return result;
                }

                if (oCompany != null)
                {
                    if (oCompany.Connected == false)
                    {
                        Dades.oCompany = oCompany;
                        return oCompany.GetLastErrorDescription();
                    }
                    else
                    {
                        Dades.oCompany = oCompany;

                        string jsonUser = GetUserInfo(user);

                        List<mdUsuari> lstjsonUser = (List<mdUsuari>)JsonConvert.DeserializeObject(jsonUser, typeof(List<mdUsuari>));

                        mdUsuari mdUser = lstjsonUser[0];

                        userToken = mdUser.USER_CODE + "#" + mdUser.USERID + "#" + DateTime.UtcNow.AddHours(18).ToString("yyyyMMddHHmmss"); // USER_CODE # USERID # FechaCaducidad
                        encryptedUserToken = encrypt.AES256_Encrypt(encrypt.AES256_USER_Key, userToken);
                        return encryptedUserToken;
                    }
                }
                else
                {
                    return "No hi ha connexio amb la base de dades!";
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";        // -3 = Error
            }
        }

        public string GetUserInfo(string user)
        {
            try
            {
                string query = @"
                SELECT
                    T0.USERID,
                    T0.USER_CODE,
                    T0.U_NAME,
                    T1.govID,
                    T1.lastName,
                    T1.firstName,
                    T0.E_Mail,

                    -- Default group (OUDG)
                    T0.DfltsGroup       AS DefaultGroupCode,
                    T2.Name             AS DefaultGroupName,

                    -- ✅ User Group (Authorization Group)
                    U7.GroupId          AS UserGroupId,
                    G.GroupName         AS UserGroupName

                FROM OUSR T0
                LEFT JOIN OHEM T1 ON T0.USERID = T1.userId
                LEFT JOIN OUDG T2 ON T0.DfltsGroup = T2.Code
                LEFT JOIN USR7 U7 ON U7.UserId = T0.USERID
                LEFT JOIN OUGR G  ON G.GroupId = U7.GroupId

                WHERE (T0.USER_CODE = @user OR T1.govID = @user OR T0.E_Mail = @user);
                ";

                string[] parametres = { $"user:{user}" };
                DataTable dt = DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, query, parametres);

                // Afegim SapLicense (si tenim B1Upf.xml disponible)
                try
                {
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        if (!dt.Columns.Contains("SapLicense"))
                            dt.Columns.Add("SapLicense", typeof(string));

                        string userCode = dt.Rows[0]["USER_CODE"]?.ToString() ?? "";
                        dt.Rows[0]["SapLicense"] = TryGetSapLicenseFromB1Upf(userCode) ?? "";
                    }
                }
                catch
                {
                    // No trenquem el login si falla la lectura del B1Upf.xml
                }

                var jsonUser = JsonConvert.SerializeObject(dt);
                return jsonUser;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public string GetLoginToken(string user, string password, string bbdd)
        {
            Encryption encrypt = new Encryption();
            string fecha = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string tokenLogin = encrypt.AES256_Encrypt(encrypt.AES256_LOGIN_Key, fecha + '#' + user + '#' + password + '#' + bbdd);
            return tokenLogin;
        }

        public void Release(params object[] objects)
        {
            foreach (var obj in objects)
            {
                ReleaseOne(obj);
            }
        }

        private bool NotComObj(object o)
        {
            return !"System.__ComObject".Equals(o.GetType().ToString());
        }

        private void ReleaseOne(object o)
        {
            if (o == null || NotComObj(o))
            {
                return;
            }

            Marshal.ReleaseComObject(o);
        }

        // ✅ Helper d'autorització per endpoints 'write'
        // Retorna true si el userToken SAP pertany a grup "Admin" o "Advanced" (USR7/OUGR).
        public bool IsAdminOrAdvancedFromToken(string userToken)
        {
            try
            {
                if (!ValidateUserToken(userToken)) return false;

                var encrypt = new Encryption();
                userToken = encrypt.TokenModify(userToken);
                string decoded = encrypt.AES256_Decrypt(encrypt.AES256_USER_Key, userToken);
                var parts = decoded.Split('#');
                if (parts.Length < 3) return false;

                // Format token SAP: USER_CODE#USERID#yyyyMMddHHmmss
                if (!int.TryParse(parts[1], out int userId) || userId <= 0) return false;

                // ✅ IMPORTANT: sense context DB NO autoritzem, i NO fem cap fallback a SBO_DOMENJO
                if (string.IsNullOrWhiteSpace(Dades.DOMENJO_BBDD))
                    return false;

                // Ens assegurem de tenir ConnectionStringDOMENJO muntada
                if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
                {
                    Dades.SetupDades();
                    if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
                        return false;
                }

                string sql = @"
                    SELECT G.GroupName
                    FROM USR7 U7
                    INNER JOIN OUGR G ON G.GroupId = U7.GroupId
                    WHERE U7.UserId = @UserId;
                ";

                var prm = new List<SqlParameter>
                {
                    new SqlParameter("@UserId", SqlDbType.Int) { Value = userId }
                };

                var dt = DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, sql, prm);

                if (dt == null || dt.Rows.Count == 0) return false;

                foreach (DataRow rr in dt.Rows)
                {
                    var g = rr["GroupName"]?.ToString() ?? "";
                    if (g.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                        g.Equals("Advanced", StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        // [1B.1] Llegeix el B1Upf.xml i retorna el KeyDesc “principal” per l’usuari
        private string? TryGetSapLicenseFromB1Upf(string userCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userCode)) return null;

                // IMPORTANT: no suposem ruta. La definim per variable d’entorn.
                // Ex: B1UPF_XML_PATH = C:\...\B1Upf.xml
                string? path = Environment.GetEnvironmentVariable("B1UPF_XML_PATH");
                if (string.IsNullOrWhiteSpace(path)) return null;
                if (!File.Exists(path)) return null;

                var doc = XDocument.Load(path);

                var userNode = doc
                    .Descendants("User")
                    .FirstOrDefault(u =>
                        string.Equals((string?)u.Element("UserName"), userCode, StringComparison.OrdinalIgnoreCase));

                if (userNode == null) return null;

                var keyDescs = userNode
                    .Descendants("Module")
                    .Select(m => (string?)m.Element("KeyDesc"))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!.Trim())
                    .Distinct()
                    .ToList();

                if (keyDescs.Count == 0) return null;

                // Preferim una que contingui “SAP Business One”; sinó la primera
                return keyDescs.FirstOrDefault(x => x.Contains("SAP Business One", StringComparison.OrdinalIgnoreCase))
                       ?? keyDescs.First();
            }
            catch
            {
                return null;
            }
        }
    }
}
