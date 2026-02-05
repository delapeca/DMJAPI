using Microsoft.Data.SqlClient;

namespace XNDmjApi.Functions
{
    public class MSSQL
    {
        string cadenaConnexio;
        SqlConnection connSQL;

        public SqlConnection connexioMSSQL()
        {
            cadenaConnexio = Dades.ConnectionStringDOMENJO;
            connSQL = new SqlConnection(cadenaConnexio);

            try
            {
                connSQL.Open();

                if (connSQL.State == System.Data.ConnectionState.Open)
                {
                    return connSQL;
                }

            }
            catch (SqlException ex)
            {
                //Debug.Print(ex.Message);
                return null;
            }
            return null;
        }

        public SqlConnection connexioPROPIES()
        {
            cadenaConnexio = Dades.ConnectionStringPROPIES;
            connSQL = new SqlConnection(cadenaConnexio);

            try
            {
                connSQL.Open();

                if (connSQL.State == System.Data.ConnectionState.Open)
                {
                    return connSQL;
                }

            }
            catch (SqlException ex)
            {
                //Debug.Print(ex.Message);
                return null;
            }
            return null;
        }

        public SqlConnection connexioCOMMON()
        {
            cadenaConnexio = Dades.ConnectionStringCOMMON;
            connSQL = new SqlConnection(cadenaConnexio);

            try
            {
                connSQL.Open();

                if (connSQL.State == System.Data.ConnectionState.Open)
                {
                    return connSQL;
                }

            }
            catch (SqlException ex)
            {
                //Debug.Print(ex.Message);
                return null;
            }
            return null;
        }
    }
}
