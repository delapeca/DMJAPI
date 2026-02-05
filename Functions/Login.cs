using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System.Data;
using XNDmjApi.Models;

namespace XNDmjApi.Functions
{
    public class Login
    {

        //public Login(IConfiguration configuration)
        //{
        //    Configuration = configuration;
        //}

        public string LoginByToken(string loginToken, string role = null)
        {
            try
            {
                // 0️⃣ Assegurem que Dades té la connexió muntada
                if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
                {
                    Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                    Dades.SetupDades();
                }

                Encryption encrypt = new Encryption();
                string userToken = "";
                string encryptedUserToken = "";

                string tokenDecoded = encrypt.AES256_Decrypt(encrypt.AES256_LOGIN_Key, loginToken);
                string date = tokenDecoded.Split('#')[0];
                string email = tokenDecoded.Split('#')[1];
                string password = tokenDecoded.Split('#')[2];

                // Validar fecha
                DateTime loginDate = DateTime.ParseExact(
                    date,
                    "yyyyMMddHHmmss",
                    System.Globalization.CultureInfo.InvariantCulture
                );

                // 2 hores de caducitat (com abans)
                if (DateTime.UtcNow.Subtract(loginDate).TotalSeconds >= 7200)
                {
                    return "-1";    // -1 = Límite de tiempo excedido
                }

                // 1️⃣ SQL base
                string SQL = "SELECT * FROM [dbo].[@XNUSERWEB] WHERE U_Email=@Email AND U_Password=@Password";

                // 2️⃣ Si ens han passat role, filtrem també per U_Rol
                if (!string.IsNullOrEmpty(role))
                {
                    SQL += " AND U_Rol=@Role";
                }

                List<SqlParameter> prm = new List<SqlParameter>();

                // @Email com a varchar(250)
                var pEmail = new SqlParameter("@Email", SqlDbType.VarChar, 250) { Value = email };
                prm.Add(pEmail);

                // Convertim el hash HEX (EF79...) a byte[] per al varbinary
                int len = password.Length / 2;
                byte[] passwordBytes = new byte[len];
                for (int i = 0; i < len; i++)
                {
                    passwordBytes[i] = Convert.ToByte(password.Substring(i * 2, 2), 16);
                }

                // @Password com a varbinary
                var pPass = new SqlParameter("@Password", SqlDbType.VarBinary, passwordBytes.Length)
                {
                    Value = passwordBytes
                };
                prm.Add(pPass);

                // @Role (si ve informat)
                if (!string.IsNullOrEmpty(role))
                {
                    var pRole = new SqlParameter("@Role", SqlDbType.VarChar, 50) { Value = role };
                    prm.Add(pRole);
                }

                DataTable dt = DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, SQL, prm);

                if (dt.Rows.Count > 0)
                {
                    // Obtenim el token d'usuari (igual que abans)
                    userToken = dt.Rows[0]["U_Email"].ToString()
                              + "#"
                              + DateTime.UtcNow.AddHours(18).ToString("yyyyMMddHHmmss");

                    encryptedUserToken = encrypt.AES256_Encrypt(encrypt.AES256_USER_Key, userToken);
                    return encryptedUserToken;
                }
                else
                {
                    return "-2";    // -2 = Usuario o clave incorrectas (o rol no coincideix)
                }
            }
            catch (Exception ex)
            {
                try
                {
                    string logDir = @"C:\Temp";
                    string logFile = System.IO.Path.Combine(logDir, "DmjApi_Login_Errors.log");

                    System.IO.Directory.CreateDirectory(logDir);

                    System.IO.File.AppendAllText(
                        logFile,
                        DateTime.Now.ToString("s") + " - LoginByToken ERROR: " + ex.ToString() + Environment.NewLine
                    );
                }
                catch
                {
                    // No fem res si falla el log; no volem trencar encara més el login
                }

                return "-3";        // -3 = Error (mateix contracte amb el client)
            }

        }

        public string getTokenLogin(string email, string password) 
        {
            Encryption encrypt = new Encryption();
            string fecha = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string passwordEncrypted = encrypt.GetSHA256(password);
            string dades = fecha + '#' + email + '#' + passwordEncrypted;
            string tokenLogin = encrypt.AES256_Encrypt(encrypt.AES256_USER_Key, dades);
            return tokenLogin;
        }

        public bool SetPassword(string userToken, string encryptedOldPassword, string encryptedNewPassword)
        {
            try
            {
                if (!ValidateUserToken(userToken))
                {
                    return false;
                }
                string emailUsuario = this.GetEmailUserFromToken(userToken);

                Encryption encrypt = new Encryption();
                string oldPassword = encrypt.AES256_Decrypt(encrypt.AES256_LOGIN_Key, encryptedOldPassword);
                string newPassword = encrypt.AES256_Decrypt(encrypt.AES256_LOGIN_Key, encryptedNewPassword);


                List<SqlParameter> SqlParams = new List<SqlParameter>();
                SqlParameter p = new SqlParameter();

                p = new SqlParameter(); p.ParameterName = "Email"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = emailUsuario; SqlParams.Add(p);
                p = new SqlParameter(); p.ParameterName = "OldPassword"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = oldPassword; SqlParams.Add(p);
                p = new SqlParameter(); p.ParameterName = "NewPassword"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = newPassword; SqlParams.Add(p);

                DataTable dt = DataAccess.ExecuteStoredProcedure(Dades.ConnectionStringDOMENJO, "dbo.XN_SetWebUserPassword", SqlParams.ToArray());
                // Obtener el resultado del SP
                if (dt.Rows[0][0].ToString() == "1")
                    return true;
                else
                    return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool ValidateUserToken(string userToken)
        {
            try
            {
                var encrypt = new Encryption();
                userToken = encrypt.TokenModify(userToken);

                // Pot ser:
                // - antic:  email#yyyyMMddHHmmss
                // - SAP:    USER_CODE#USERID#yyyyMMddHHmmss
                string decoded = encrypt.AES256_Decrypt(encrypt.AES256_USER_Key, userToken);
                var parts = decoded.Split('#');

                if (parts.Length < 2) return false;

                string fecha = null;

                if (parts.Length >= 3)
                {
                    // Token SAP
                    fecha = parts[2];
                }
                else
                {
                    // Token antic
                    fecha = parts[1];
                }

                DateTime fechaCaducidad = DateTime.ParseExact(
                    fecha,
                    "yyyyMMddHHmmss",
                    System.Globalization.CultureInfo.InvariantCulture
                );

                return DateTime.UtcNow <= fechaCaducidad;
            }
            catch
            {
                return false;
            }
        }

        public string CreateNewUser(string token)
        {
            try
            {
                // 0️⃣ Assegurem que Dades té la connexió muntada
                if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
                {
                    Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                    Dades.SetupDades();
                }

                Encryption encrypt = new Encryption();

                // 1️⃣ Desxifrar el token rebut des de Laravel
                string tokenDecoded = encrypt.AES256_Decrypt(encrypt.AES256_USER_Key, token);

                var parts = tokenDecoded.Split('#');
                if (parts.Length < 6)
                {
                    throw new Exception("Token amb format incorrecte: " + tokenDecoded);
                }

                string email = parts[0];
                string dni = parts[1];
                string password = parts[2];
                string role = parts[3];
                string docNum = parts[4];
                string docDate = parts[5];

                // 2️⃣ Paràmetres per a la SP XN_InsertWebUser
                List<SqlParameter> SqlParams = new List<SqlParameter>();
                SqlParameter p;

                p = new SqlParameter("Email", SqlDbType.VarChar) { Value = email }; SqlParams.Add(p);
                p = new SqlParameter("Password", SqlDbType.VarChar) { Value = password }; SqlParams.Add(p);
                p = new SqlParameter("DniNifCif", SqlDbType.VarChar) { Value = dni }; SqlParams.Add(p);
                p = new SqlParameter("Rol", SqlDbType.VarChar) { Value = role }; SqlParams.Add(p);
                p = new SqlParameter("DocNum", SqlDbType.VarChar) { Value = docNum }; SqlParams.Add(p);
                p = new SqlParameter("DocDate", SqlDbType.VarChar) { Value = docDate }; SqlParams.Add(p);

                DataTable dt = DataAccess.ExecuteStoredProcedure(
                    Dades.ConnectionStringDOMENJO,
                    "dbo.XN_InsertWebUser",
                    SqlParams.ToArray()
                );

                if (dt.Rows.Count == 0)
                    throw new Exception("XN_InsertWebUser no ha retornat cap fila.");

                DataRow row = dt.Rows[0];

                // 3️⃣ Llegim la sortida tipificada de la SP: Success / Code / Message
                bool success = false;
                string code = null;
                string message = null;

                if (row.Table.Columns.Contains("Success") && row["Success"] != DBNull.Value)
                    success = Convert.ToInt32(row["Success"]) == 1;

                if (row.Table.Columns.Contains("Code") && row["Code"] != DBNull.Value)
                    code = row["Code"]?.ToString();

                if (row.Table.Columns.Contains("Message") && row["Message"] != DBNull.Value)
                    message = row["Message"]?.ToString();

                if (string.IsNullOrWhiteSpace(code))
                    code = success ? "OK" : "UNKNOWN";

                if (string.IsNullOrWhiteSpace(message))
                    message = success
                        ? "Usuari creat correctament."
                        : "Error desconegut en XN_InsertWebUser.";

                // 4️⃣ Preparem el payload JSON que llegirà Laravel
                var payload = new
                {
                    success = success,
                    code = code,
                    message = message
                };

                // Retornem JSON perquè el controlador el pugui enviar directament a Laravel
                return JsonConvert.SerializeObject(payload);
            }
            catch (Exception ex)
            {
                // 5️⃣ Qualsevol excepció inesperada → també en format JSON
                var errorPayload = new
                {
                    success = false,
                    code = "EXCEPTION",
                    message = ex.Message
                };

                return JsonConvert.SerializeObject(errorPayload);
            }
        }

        private string GetEmailUserFromToken(string token)
        {
            Encryption encrypt = new Encryption();
            token = encrypt.TokenModify(token);
            string tokenDescodificado = encrypt.AES256_Decrypt(encrypt.AES256_USER_Key, token);
            string emailUsuario = tokenDescodificado.Split('#')[0];
            return emailUsuario;
        }

        public User GetUserInfo(string userToken)
        {
            Encryption encrypt = new Encryption();
            userToken = encrypt.TokenModify(userToken);
            string tokenDescodificado = encrypt.AES256_Decrypt(encrypt.AES256_USER_Key, userToken);
            string emailUsuario = tokenDescodificado.Split('#')[0];
            //string fecha = tokenDescodificado.Split('#')[1];
            string SQL = $"SELECT * FROM [dbo].[@XNUSERWEB] WHERE U_Email=@Email";

            List<SqlParameter> prm = new List<SqlParameter>();
            SqlParameter param = null;

            param = new SqlParameter("@Email", emailUsuario);
            //param = new SqlParameter("@ItemCode", ItemCode.HasValue ? (object)ItemCode : DBNull.Value);
            prm.Add(param);

            DataTable dt = DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, SQL, prm);
            if (dt.Rows.Count > 0)
            {
                User Usuari = new User();

                // Obtenim el token d'usuari
                userToken = dt.Rows[0]["U_Email"].ToString() + "#" + DateTime.UtcNow.AddHours(18).ToString("yyyyMMddHHmmss");
                userToken = encrypt.AES256_Encrypt(encrypt.AES256_USER_Key, userToken);

                // Obtenim les dades de l'usuari
                Usuari.code = int.Parse(dt.Rows[0]["Code"].ToString());

                // Name (camp estàndard de la UDT)
                if (dt.Columns.Contains("Name") && dt.Rows[0]["Name"] != DBNull.Value)
                {
                    Usuari.name = dt.Rows[0]["Name"].ToString();
                }

                Usuari.email = dt.Rows[0]["U_Email"].ToString();
                Usuari.userName = dt.Rows[0]["U_UserName"].ToString();
                Usuari.cardCode = dt.Rows[0]["U_CardCode"].ToString();
                Usuari.cardName = dt.Rows[0]["U_CardName"].ToString();
                Usuari.warehouse = dt.Rows[0]["U_Warehouse"].ToString();
                Usuari.role = dt.Rows[0]["U_Rol"].ToString();
                Usuari.startDate = DateTime.Parse(dt.Rows[0]["U_StartDate"].ToString());
                Usuari.expirationDate = DateTime.Parse(dt.Rows[0]["U_ExpirationDate"].ToString());
                Usuari.isAdmin = dt.Rows[0]["U_IsAdmin"] == DBNull.Value
                ? "N"
                : dt.Rows[0]["U_IsAdmin"].ToString();
                if (dt.Rows[0]["U_LastAccess"] != DBNull.Value)
                {
                    Usuari.lastAccess = DateTime.Parse(dt.Rows[0]["U_LastAccess"].ToString());
                }

                // U_ForgotPassIdentity, si el vols exposar
                if (dt.Columns.Contains("U_ForgotPassIdentity") && dt.Rows[0]["U_ForgotPassIdentity"] != DBNull.Value)
                {
                    Usuari.forgotPassIdentity = dt.Rows[0]["U_ForgotPassIdentity"].ToString();
                }

                return Usuari;

            }
            else
            {
                return null;    // -2 = Usuario o clave incorrectas
            }
        }

        public string AdminInsertWebUser(string adminUserToken,string email,string password,string dniNifCif,int? docNum,DateTime? docDate,string role,string isAdmin)
        {
            try
            {
                // 0️⃣ Assegurem que Dades té la connexió muntada
                if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
                {
                    Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                    Dades.SetupDades();
                }

                // 1️⃣ Validar token i permisos d'empleat (token antic o token SAP)
                if (!ValidateUserToken(adminUserToken))
                {
                    var invalidToken = new
                    {
                        success = false,
                        code = "INVALID_TOKEN",
                        message = "Token d'usuari no vàlid o caducat."
                    };
                    return JsonConvert.SerializeObject(invalidToken);
                }

                // Intentem obtenir info via sistema antic (@XNUSERWEB)
                var empUser = GetUserInfo(adminUserToken);

                bool isEmployee = false;
                string empIsAdmin = "N";

                if (empUser != null)
                {
                    // Sistema antic
                    if (empUser.role == "Empleat")
                    {
                        isEmployee = true;
                        empIsAdmin = string.IsNullOrWhiteSpace(empUser.isAdmin) ? "N" : empUser.isAdmin;
                    }
                }
                else
                {
                    // Token SAP: USER_CODE#USERID#yyyyMMddHHmmss
                    try
                    {
                        var encrypt = new Encryption();
                        string tokenFixed = encrypt.TokenModify(adminUserToken);
                        string decoded = encrypt.AES256_Decrypt(encrypt.AES256_USER_Key, tokenFixed);
                        var parts = decoded.Split('#');
                        if (parts.Length >= 3)
                        {
                            isEmployee = true;

                            // parts[1] = USERID
                            int sapUserId = 0;
                            int.TryParse(parts[1], out sapUserId);

                            // Per defecte, no admin
                            empIsAdmin = "N";

                            // Si trobem grup Admin o Advanced -> el considerem "admin intranet"
                            if (sapUserId > 0)
                            {
                                try
                                {
                                    string sql = @"
                                        SELECT G.GroupName
                                        FROM USR7 U7
                                        INNER JOIN OUGR G ON G.GroupId = U7.GroupId
                                        WHERE U7.UserId = @UserId;
                                    ";

                                    var prm = new List<SqlParameter>
                                    {
                                        new SqlParameter("@UserId", SqlDbType.Int) { Value = sapUserId }
                                    };

                                    DataTable dtGrp = DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, sql, prm);

                                    if (dtGrp != null && dtGrp.Rows.Count > 0 && dtGrp.Columns.Contains("GroupName"))
                                    {
                                        foreach (DataRow rr in dtGrp.Rows)
                                        {
                                            var g = rr["GroupName"]?.ToString() ?? "";
                                            if (g.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                                                g.Equals("Advanced", StringComparison.OrdinalIgnoreCase))
                                            {
                                                empIsAdmin = "Y";
                                                break;
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    // no trenquem l'alta per això
                                }
                            }
                        }

                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (!isEmployee)
                {
                    var notAuth = new
                    {
                        success = false,
                        code = "NOT_AUTHORIZED",
                        message = "L'usuari no té permisos d'empleat."
                    };
                    return JsonConvert.SerializeObject(notAuth);
                }

                // ⚠️ Només exigim admin si s’està creant un usuari EMPLEAT
                // Amb token SAP, empIsAdmin serà 'N' i quedarà bloquejat (que és el que volem ara).
                if (string.Equals(role, "Empleat", StringComparison.OrdinalIgnoreCase) && empIsAdmin != "Y")
                {
                    var notAuth = new
                    {
                        success = false,
                        code = "NOT_AUTHORIZED",
                        message = "Només un administrador pot crear usuaris Empleat."
                    };
                    return JsonConvert.SerializeObject(notAuth);
                }



                // 2️⃣ Validacions bàsiques de les dades d'alta
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    var missing = new
                    {
                        success = false,
                        code = "MISSING_DATA",
                        message = "Cal informar com a mínim email i password."
                    };
                    return JsonConvert.SerializeObject(missing);
                }

                if (string.IsNullOrWhiteSpace(role))
                {
                    var missingRole = new
                    {
                        success = false,
                        code = "MISSING_ROLE",
                        message = "Cal informar el rol (Client / Empleat)."
                    };
                    return JsonConvert.SerializeObject(missingRole);
                }

                // 3️⃣ Normalitzem role i isAdmin (la SP ja força isAdmin = 'N' per Clients)
                string normalizedRole = role;
                string normalizedIsAdmin = string.IsNullOrWhiteSpace(isAdmin)
                    ? "N"
                    : isAdmin.ToUpperInvariant();

                if (normalizedIsAdmin != "Y" && normalizedIsAdmin != "N")
                {
                    normalizedIsAdmin = "N";
                }

                // 4️⃣ Construïm els paràmetres per XN_InsertWebUser
                var SqlParams = new List<SqlParameter>();

                SqlParams.Add(new SqlParameter("Email", SqlDbType.VarChar, 250) { Value = email });
                SqlParams.Add(new SqlParameter("Password", SqlDbType.VarChar, 32) { Value = password });
                SqlParams.Add(new SqlParameter("DniNifCif", SqlDbType.VarChar, 32) { Value = (object)dniNifCif ?? DBNull.Value });
                SqlParams.Add(new SqlParameter("DocNum", SqlDbType.Int) { Value = (object)docNum ?? DBNull.Value });
                SqlParams.Add(new SqlParameter("DocDate", SqlDbType.DateTime) { Value = (object)docDate ?? DBNull.Value });
                SqlParams.Add(new SqlParameter("Rol", SqlDbType.VarChar, 25) { Value = normalizedRole });
                SqlParams.Add(new SqlParameter("IsAdmin", SqlDbType.Char, 1) { Value = normalizedIsAdmin });

                DataTable dt = DataAccess.ExecuteStoredProcedure(
                    Dades.ConnectionStringDOMENJO,
                    "dbo.XN_InsertWebUser",
                    SqlParams.ToArray()
                );

                if (dt.Rows.Count == 0)
                {
                    var noRow = new
                    {
                        success = false,
                        code = "NO_RESULT",
                        message = "XN_InsertWebUser no ha retornat cap fila."
                    };
                    return JsonConvert.SerializeObject(noRow);
                }

                DataRow row = dt.Rows[0];

                bool success = false;
                string code = null;
                string message = null;

                if (row.Table.Columns.Contains("Success") && row["Success"] != DBNull.Value)
                    success = Convert.ToInt32(row["Success"]) == 1;

                if (row.Table.Columns.Contains("Code") && row["Code"] != DBNull.Value)
                    code = row["Code"]?.ToString();

                if (row.Table.Columns.Contains("Message") && row["Message"] != DBNull.Value)
                    message = row["Message"]?.ToString();

                if (string.IsNullOrWhiteSpace(code))
                    code = success ? "OK" : "UNKNOWN";

                if (string.IsNullOrWhiteSpace(message))
                    message = success
                        ? "Usuari creat correctament."
                        : "Error desconegut en XN_InsertWebUser.";

                var payload = new
                {
                    success = success,
                    code = code,
                    message = message
                };

                return JsonConvert.SerializeObject(payload);
            }
            catch (Exception ex)
            {
                var errorPayload = new
                {
                    success = false,
                    code = "EXCEPTION",
                    message = ex.Message
                };

                return JsonConvert.SerializeObject(errorPayload);
            }
        }

    }
}
