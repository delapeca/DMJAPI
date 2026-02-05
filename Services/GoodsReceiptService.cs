using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using SAPbobsCOM;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using XNDmjApi.Functions;
using XNDmjApi.Models;

namespace XNDmjApi.Services
{
    public class GoodsReceiptService
    {
        Company oCompany;
        SAPBO SAP = new SAPBO();
        //public string setGoodsReceipt(string jsonString)
        //{

        //    //DataTable dtReception = (DataTable)JsonConvert.DeserializeObject(jsonString, (typeof(DataTable)));

        //    //Dades.COMPANY_SAP = "XAVDATOS";
        //    //Dades.USER_SAP = "admin1";
        //    //Dades.PWD_SAP = "master";
        //    Dades.SetupDades();
        //    oCompany = SAP.ConnexioSAPBO();

        //    if (oCompany.Connected)
        //    {

        //        SAPbobsCOM.Documents oPurchaseDelivery = (SAPbobsCOM.Documents)oCompany.GetBusinessObject(SAPbobsCOM.BoObjectTypes.oPurchaseDeliveryNotes);

        //        //oPurchaseDelivery.CardCode = dtReception.Rows[0]["cardCode"].ToString();
        //        //oPurchaseDelivery.CardCode = "P000121";
        //        //oPurchaseDelivery.DocDueDate = DateTime.Now;
        //        oPurchaseDelivery.Comments = "Based On Purchase Orders - "; // + PO.ToString();
        //        oPurchaseDelivery.JournalMemo = "Goods Receipt PO - "; // + receipts.CardCode;

        //        //foreach (DataRow dr in dtReception.Rows)
        //        //{
        //        //oPurchaseDelivery.Lines.SetCurrentLine(0);
        //        //oPurchaseDelivery.Lines.BaseType = (int)SAPbobsCOM.BoObjectTypes.oPurchaseOrders;
        //        //oPurchaseDelivery.Lines.BaseEntry = 1311; //the number of base order
        //        //oPurchaseDelivery.Lines.BaseLine = 0; //the index of line in base order, that you need copy
        //        //oPurchaseDelivery.Lines.SerialNumbers.SetCurrentLine(i);
        //        //oPurchaseDelivery.Lines.SerialNumbers.Add();
        //        //oPurchaseDelivery.Lines.ItemCode = "EI107271";
        //        //oPurchaseDelivery.Lines.ItemDescription = oItems.ItemName;
        //        //oPurchaseDelivery.Lines.WarehouseCode = "01";
        //        //oPurchaseDelivery.Lines.Currency = CurrencyComboBox.SelectedItem;
        //        //oPurchaseDelivery.Lines.Price = CDbl(PriceText.Text);
        //        //oPurchaseDelivery.Lines.Rate = 2;
        //        //oPurchaseDelivery.Lines.ShipDate = Now;
        //        //oPurchaseDelivery.Lines.Quantity = CDbl(QuantityText.Text);
        //        //oPurchaseDelivery.Lines.DiscountPercent = CDbl(DiscountText.Text);
        //        //oPurchaseDelivery.PaymentGroupCode = -1;
        //        //oPurchaseDelivery.DocTotal = 1;

        //        //}


        //        oPurchaseDelivery.CardCode = "P000529";
        //        oPurchaseDelivery.DocDueDate = DateTime.Now;

        //        oPurchaseDelivery.Lines.SetCurrentLine(0);
        //        oPurchaseDelivery.Lines.BaseType = (int)SAPbobsCOM.BoObjectTypes.oPurchaseOrders;
        //        oPurchaseDelivery.Lines.BaseEntry = 1437; //the number of base order
        //        oPurchaseDelivery.Lines.BaseLine = 0; //the index of line in base order, that you need copy
        //        oPurchaseDelivery.Lines.Quantity = 1;


        //        oPurchaseDelivery.Lines.Add();
        //        oPurchaseDelivery.Lines.BaseType = (int)SAPbobsCOM.BoObjectTypes.oPurchaseOrders;
        //        oPurchaseDelivery.Lines.BaseEntry = 1437; //the number of base order
        //        oPurchaseDelivery.Lines.BaseLine = 2; //the index of line in base order, that you need copy
        //        oPurchaseDelivery.Lines.Quantity = 10;


        //        oPurchaseDelivery.Lines.Add();
        //        oPurchaseDelivery.Lines.BaseType = (int)SAPbobsCOM.BoObjectTypes.oPurchaseOrders;
        //        oPurchaseDelivery.Lines.BaseEntry = 1437; //the number of base order
        //        oPurchaseDelivery.Lines.BaseLine = 3; //the index of line in base order, that you need copy
        //        oPurchaseDelivery.Lines.Quantity = 5;


        //        if (oPurchaseDelivery.Add() != 0)
        //        {

        //            return oCompany.GetLastErrorDescription();
        //        }
        //        else
        //        {
        //            return ("PurchaseDelivery [" + oCompany.GetNewObjectKey() + "] created!");
        //        }
        //    }
        //    else
        //    {
        //        return "No hi ha connexio!";
        //    }
        //}

        public string getItem()
        {
            Dades.SetupDades();
            oCompany =  SAP.ConnexioSAPBO();

            if (oCompany.Connected)
            {
                SAPbobsCOM.Items oItem = oCompany.GetBusinessObject(BoObjectTypes.oItems);
                oItem.GetByKey("AI100001");
                return oItem.ItemName;
            }
            else
            {
                return "No hi ha connexio!";
            }
        }

        public string setTemporalGoodReceipt(string jsonTGR)
        {
            Dades.SetupDades();
            
            try
            {
                mdTemporalGoodReceipt mdTGR = new mdTemporalGoodReceipt();
                if (jsonTGR != null)
                {
                    // Passar json a Object
                    mdTGR = JsonConvert.DeserializeObject<mdTemporalGoodReceipt>(jsonTGR);

                    string sql = $"INSERT INTO [dbo].[@XNTMPGOODRECEIPT] ([U_empID] ,[U_Data] ,[U_DocDate] ,[U_DocNum] ,[U_DocEntry] ,[U_CardCode] ,[U_CardName] ,[U_LineNum] ,[U_ItemCode] ,[U_Dscription] ,[U_RealQuantity] ,[U_ProposedQuantity] ,[U_Price] ,[U_DiscPrcnt] ,[U_LineStatus] ,[U_LineObservation]) " +
                        "VALUES (@empID ,@Data ,@DocDate ,@DocNum ,@DocEntry ,@CardCode ,@CardName ,@LineNum ,@ItemCode ,@Dscription ,@RealQuantity ,@ProposedQuantity ,@Price ,@DiscPrcnt ,@LineStatus ,@LineObservation)";

                    List<SqlParameter> SqlParams = new List<SqlParameter>();
                    SqlParameter p = new SqlParameter();

                    p = new SqlParameter(); p.ParameterName = "@empID"; p.SqlDbType = System.Data.SqlDbType.Int; p.SqlValue = mdTGR.empID; SqlParams.Add(p);
                    p = new SqlParameter(); p.ParameterName = "@Data"; p.SqlDbType = System.Data.SqlDbType.DateTime; p.SqlValue = mdTGR.data.Date; SqlParams.Add(p);
                    p = new SqlParameter(); p.ParameterName = "@DocDate"; p.SqlDbType = System.Data.SqlDbType.DateTime; p.SqlValue = mdTGR.DocDate.Date; SqlParams.Add(p);
                    p = new SqlParameter(); p.ParameterName = "@DocNum"; p.SqlDbType = System.Data.SqlDbType.Int; p.SqlValue = mdTGR.DocNum; SqlParams.Add(p);
                    p = new SqlParameter(); p.ParameterName = "@DocEntry"; p.SqlDbType = System.Data.SqlDbType.Int; p.SqlValue = mdTGR.DocEntry; SqlParams.Add(p);
                    p = new SqlParameter(); p.ParameterName = "@CardCode"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = mdTGR.CardCode; SqlParams.Add(p);
                    p = new SqlParameter(); p.ParameterName = "@CardName"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = mdTGR.CardName; SqlParams.Add(p);
                    p = new SqlParameter(); p.ParameterName = "@LineNum"; p.SqlDbType = System.Data.SqlDbType.Int; p.SqlValue = mdTGR.LineNum; SqlParams.Add(p);
                    p = new SqlParameter(); p.ParameterName = "@ItemCode"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = mdTGR.ItemCode; SqlParams.Add(p);
                    p = new SqlParameter(); p.ParameterName = "@Dscription"; p.SqlDbType = System.Data.SqlDbType.VarChar; p.SqlValue = mdTGR.Dscription; SqlParams.Add(p);
                    p = new SqlParameter(); p.ParameterName = "@RealQuantity"; p.SqlDbType = System.Data.SqlDbType.Float; p.SqlValue = mdTGR.RealQuantity; SqlParams.Add(p);
                    p = new SqlParameter(); p.ParameterName = "@ProposedQuantity"; p.SqlDbType = System.Data.SqlDbType.Float; p.SqlValue = mdTGR.ProposedQuantity; SqlParams.Add(p);
                    p = new SqlParameter(); p.ParameterName = "@Price"; p.SqlDbType = System.Data.SqlDbType.Float; p.SqlValue = mdTGR.Price; SqlParams.Add(p);
                    p = new SqlParameter(); p.ParameterName = "@DiscPrcnt"; p.SqlDbType = System.Data.SqlDbType.Float; p.SqlValue = mdTGR.DiscPrcnt; SqlParams.Add(p);
                    p = new SqlParameter(); p.ParameterName = "@LineStatus"; p.SqlDbType = System.Data.SqlDbType.Int; p.SqlValue = 0; SqlParams.Add(p);
                    p = new SqlParameter(); p.ParameterName = "@LineObservation"; p.SqlDbType = System.Data.SqlDbType.Int; p.SqlValue = 0; SqlParams.Add(p);

                    string result = DataAccess.ExecuteQuery(Dades.ConnectionStringDOMENJO, sql, SqlParams.ToArray());

                    return $"OK#{result}#{Dades.ConnectionStringDOMENJO}";
                }
                return $"ERROR#invalid Json#{jsonTGR}";
            } catch (Exception e)
            {
                return $"ERROR#{e.Message}#{jsonTGR}";
            }
            
        }

        public string setGoodsReceipt(string jsonGR)
        {
            // Json de prova
            //jsonGR = "[{ 'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-10-21T00:00:00','DocNum':24001546,'Warehouse':null,'CardCode':'P000494','CardName':'ABRATOOLS, S.A.','LineNum':3,'ItemCode':'FE103517','Dscription':'TOLDO RAFIA 6X10M.','RealQuantity':10.0,'ProposedQuantity':10.0,'Price':12.217,'DiscPrcnt':34.0,'Selected':true},{ 'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-10-31T00:00:00','DocNum':24001618,'Warehouse':null,'CardCode':'P000494','CardName':'ABRATOOLS, S.A.','LineNum':13,'ItemCode':'EI108536','Dscription':'COMPRESOR BEELFLEX BF3 SILENT 230V-F1 1,2HP','RealQuantity':2.0,'ProposedQuantity':2.0,'Price':78.0,'DiscPrcnt':0.0,'Selected':true},{ 'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-10-31T00:00:00','DocNum':24001618,'Warehouse':null,'CardCode':'P000494','CardName':'ABRATOOLS, S.A.','LineNum':14,'ItemCode':'EI108537','Dscription':'COMPRESOR BEELFLEX BF8 SILENT 230V-F1 1,2HP','RealQuantity':2.0,'ProposedQuantity':2.0,'Price':81.0,'DiscPrcnt':0.0,'Selected':true},{ 'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-14T00:00:00','DocNum':24001708,'Warehouse':null,'CardCode':'P000494','CardName':'ABRATOOLS, S.A.','LineNum':3,'ItemCode':'FE103511','Dscription':'TOLDO RAFIA 10X15','RealQuantity':4.0,'ProposedQuantity':4.0,'Price':46.26,'DiscPrcnt':0.0,'Selected':true},{ 'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-22T00:00:00','DocNum':24001767,'Warehouse':null,'CardCode':'P000010','CardName':'VELUX SPAIN, S.A.','LineNum':0,'ItemCode':'FU100112','Dscription':'DKL 101 1085S CORTINA OSCURECIMIENTO MANUAL GAMA ESTANDAR','RealQuantity':3.0,'ProposedQuantity':3.0,'Price':68.0,'DiscPrcnt':0.0,'Selected':true},{ 'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-22T00:00:00','DocNum':24001767,'Warehouse':null,'CardCode':'P000010','CardName':'VELUX SPAIN, S.A.','LineNum':1,'ItemCode':'FU102805','Dscription':'DSL PK04 1085S CORTINA DE OSCURECIMIENTO SOLAR GAMA ESTANDAR','RealQuantity':1.0,'ProposedQuantity':1.0,'Price':190.0,'DiscPrcnt':0.0,'Selected':true},{ 'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-12-03T00:00:00','DocNum':24001826,'Warehouse':null,'CardCode':'P000010','CardName':'VELUX SPAIN, S.A.','LineNum':17,'ItemCode':'FU102463','Dscription':'GGL MK04 3070 VENTANA GIRATORIA MADERA VIDRIO 70','RealQuantity':2.0,'ProposedQuantity':4.0,'Price':318.24,'DiscPrcnt':22.0,'Selected':true},{ 'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-12-03T00:00:00','DocNum':24001830,'Warehouse':null,'CardCode':'P002733','CardName':'SOUDAL QUIMICA S.L','LineNum':0,'ItemCode':'FE101390','Dscription':'ESPUMA POLIURETANO  PISTOLA 750ML.','RealQuantity':120.0,'ProposedQuantity':120.0,'Price':3.12,'DiscPrcnt':0.0,'Selected':true},{ 'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-12-03T00:00:00','DocNum':24001830,'Warehouse':null,'CardCode':'P002733','CardName':'SOUDAL QUIMICA S.L','LineNum':1,'ItemCode':'FE101387','Dscription':'ESPUMA POLIURETANO TEJAS PISTOLA 750ML','RealQuantity':120.0,'ProposedQuantity':120.0,'Price':3.79,'DiscPrcnt':0.0,'Selected':true}]";
            jsonGR = "[{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-05T00:00:00','DocNum':24001635,'DocEntry':1635,'Warehouse':null,'CardCode':'P000445','CardName':'GRONPES, S.L.','LineNum':1,'ItemCode':'FE100469','Dscription':'BOTAS GOMA ALTAS SEGURIDAD 41','RealQuantity':10.0,'ProposedQuantity':10.0,'Price':10.95,'DiscPrcnt':0.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-05T00:00:00','DocNum':24001635,'DocEntry':1635,'Warehouse':null,'CardCode':'P000445','CardName':'GRONPES, S.L.','LineNum':2,'ItemCode':'FE100470','Dscription':'BOTAS GOMA ALTAS SEGURIDAD 42','RealQuantity':10.0,'ProposedQuantity':10.0,'Price':10.95,'DiscPrcnt':0.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-05T00:00:00','DocNum':24001635,'DocEntry':1635,'Warehouse':null,'CardCode':'P000445','CardName':'GRONPES, S.L.','LineNum':4,'ItemCode':'FE100472','Dscription':'BOTAS GOMA ALTAS SEGURIDAD 44','RealQuantity':10.0,'ProposedQuantity':10.0,'Price':10.95,'DiscPrcnt':0.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-05T00:00:00','DocNum':24001635,'DocEntry':1635,'Warehouse':null,'CardCode':'P000445','CardName':'GRONPES, S.L.','LineNum':7,'ItemCode':'FE100474','Dscription':'BOTAS GOMA ALTAS T-40 6315','RealQuantity':10.0,'ProposedQuantity':10.0,'Price':6.6,'DiscPrcnt':0.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-05T00:00:00','DocNum':24001635,'DocEntry':1635,'Warehouse':null,'CardCode':'P000445','CardName':'GRONPES, S.L.','LineNum':8,'ItemCode':'FE100475','Dscription':'BOTAS GOMA ALTAS T-41 6315','RealQuantity':10.0,'ProposedQuantity':10.0,'Price':6.6,'DiscPrcnt':0.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-12-03T00:00:00','DocNum':24001829,'DocEntry':1829,'Warehouse':null,'CardCode':'P000454','CardName':'COMERCIAL DE IMPERME.CIDAC, S.L.','LineNum':1,'ItemCode':'FE100984','Dscription':'COLA BUTILO AC-221/5L.','RealQuantity':4.0,'ProposedQuantity':4.0,'Price':26.3,'DiscPrcnt':0.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-04T00:00:00','DocNum':24001625,'DocEntry':1625,'Warehouse':null,'CardCode':'P000637','CardName':'ANDRES MALDONADO, S.A.','LineNum':8,'ItemCode':'FE113145','Dscription':'CERRADURA BUZON 47/19 CROMADA','RealQuantity':12.0,'ProposedQuantity':12.0,'Price':11.256,'DiscPrcnt':35.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-04T00:00:00','DocNum':24001625,'DocEntry':1625,'Warehouse':null,'CardCode':'P000637','CardName':'ANDRES MALDONADO, S.A.','LineNum':9,'ItemCode':'FE113286','Dscription':'CERRADURA BUZON 47/19 PULIDA','RealQuantity':12.0,'ProposedQuantity':12.0,'Price':10.172,'DiscPrcnt':35.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-04T00:00:00','DocNum':24001625,'DocEntry':1625,'Warehouse':null,'CardCode':'P000637','CardName':'ANDRES MALDONADO, S.A.','LineNum':10,'ItemCode':'FE107757','Dscription':'CERRADURA COMPAÑIA ELECT.ARMARIO CFE 20-30','RealQuantity':12.0,'ProposedQuantity':12.0,'Price':14.01,'DiscPrcnt':0.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-04T00:00:00','DocNum':24001625,'DocEntry':1625,'Warehouse':null,'CardCode':'P000637','CardName':'ANDRES MALDONADO, S.A.','LineNum':11,'ItemCode':'FE107756','Dscription':'CERRADURA COMPAÑIA ELECT.SOBREP. LT CFE 1130','RealQuantity':12.0,'ProposedQuantity':12.0,'Price':15.65,'DiscPrcnt':0.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-04T00:00:00','DocNum':24001625,'DocEntry':1625,'Warehouse':null,'CardCode':'P000637','CardName':'ANDRES MALDONADO, S.A.','LineNum':12,'ItemCode':'FE110305','Dscription':'CERRADURA CORTAFUEGO CF5ENGTR9ZCE','RealQuantity':12.0,'ProposedQuantity':12.0,'Price':20.345,'DiscPrcnt':35.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-04T00:00:00','DocNum':24001625,'DocEntry':1625,'Warehouse':null,'CardCode':'P000637','CardName':'ANDRES MALDONADO, S.A.','LineNum':13,'ItemCode':'FE109337','Dscription':'CERRADURA DE GANCHO P/PUERTA CORREDERA 4370065 SC','RealQuantity':3.0,'ProposedQuantity':3.0,'Price':25.82,'DiscPrcnt':35.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-10-11T00:00:00','DocNum':24001482,'DocEntry':1482,'Warehouse':null,'CardCode':'P002546','CardName':'CENTRO VENTAS INTERN S.A(GRUPO CEVIK)','LineNum':0,'ItemCode':'EI107970','Dscription':'DISCO LIJA OXY 225MM G.220 OXYDF225220','RealQuantity':12.0,'ProposedQuantity':12.0,'Price':3.642,'DiscPrcnt':30.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-14T00:00:00','DocNum':24001710,'DocEntry':1710,'Warehouse':null,'CardCode':'P002546','CardName':'CENTRO VENTAS INTERN S.A(GRUPO CEVIK)','LineNum':3,'ItemCode':'EI107967','Dscription':'DISCO LIJA OXY 225MM G.120 OXYDF225120','RealQuantity':24.0,'ProposedQuantity':24.0,'Price':3.64,'DiscPrcnt':30.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-27T00:00:00','DocNum':24001799,'DocEntry':1799,'Warehouse':null,'CardCode':'P002546','CardName':'CENTRO VENTAS INTERN S.A(GRUPO CEVIK)','LineNum':0,'ItemCode':'EI107967','Dscription':'DISCO LIJA OXY 225MM G.120 OXYDF225120','RealQuantity':20.0,'ProposedQuantity':20.0,'Price':3.642,'DiscPrcnt':30.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-11-27T00:00:00','DocNum':24001799,'DocEntry':1799,'Warehouse':null,'CardCode':'P002546','CardName':'CENTRO VENTAS INTERN S.A(GRUPO CEVIK)','LineNum':1,'ItemCode':'EI107970','Dscription':'DISCO LIJA OXY 225MM G.220 OXYDF225220','RealQuantity':20.0,'ProposedQuantity':20.0,'Price':3.642,'DiscPrcnt':30.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-12-16T00:00:00','DocNum':24001885,'DocEntry':1885,'Warehouse':null,'CardCode':'P003112','CardName':'AMILIBIA Y DE LA IGLESIA, S.A.','LineNum':12,'ItemCode':'FU101960','Dscription':'GOZNE GOTICO 7030-400 NEGRO','RealQuantity':8.0,'ProposedQuantity':8.0,'Price':3.362,'DiscPrcnt':28.0,'Selected':true},{'empID':1,'data':'0001-01-01T00:00:00','DocDate':'2024-09-24T00:00:00','DocNum':24001365,'DocEntry':1365,'Warehouse':null,'CardCode':'P040173','CardName':'ACQUAGAS IMPORTACIONES, S L','LineNum':0,'ItemCode':'TU101630','Dscription':'VALVULA ESFERA 1\"','RealQuantity':30.0,'ProposedQuantity':30.0,'Price':5.409,'DiscPrcnt':55.0,'Selected':true}]";
            string CardCode = null;
            int nLinia = 0;
            string Resposta = null;

            // Variables locals
            List<mdGoodsReceipt> lstGR = JsonConvert.DeserializeObject<List<mdGoodsReceipt>>(jsonGR);

            //List<mdGoodsReceipt> Proveidors = lstGR.GroupBy(x => x.CardCode).ToList<List<mdGoodsReceipt>>();

            var Proveidors = lstGR
                .GroupBy(x => x.CardCode)
                .Select (x => new
                { 
                    x.Key,
                    prov = x.ToList<mdGoodsReceipt>()
                });


            foreach (var cc in Proveidors)
            {
                Resposta += setGoodReceipt(cc.prov);
                
            }

            //foreach(var mdgr in lstGR)
            //{
            //    if (nLinia > 0)
            //    {
            //        if (CardCode != mdgr.CardCode)
            //        {
            //            nLinia = 0;
            //            Resposta += setGoodReceipt(lstGR);
            //            lstGR.Clear();
            //            lstGR = new List<mdGoodsReceipt>();
            //        }
            //    } 
            //    else
            //    {
            //        CardCode = mdgr.CardCode;
            //    }

            //    nLinia++;
            //}

            return Resposta;
        }

        private string setGoodReceipt(List<mdGoodsReceipt> lstGR)
        {
            string CardCode = "";
            int nDocEntry = 0;
            int nLinia = 0;
            int RetVal = 0;
            string Resposta = "";
            int empID = 0;
            bool ErrorsVaris = false;

            Dades.SetupDades();
            oCompany = SAP.ConnexioSAPBO();

            if (oCompany.Connected)
            {
                oCompany.StartTransaction();
                // Creem l'objecte
                SAPbobsCOM.Documents oPurchaseDelivery = oCompany.GetBusinessObject(SAPbobsCOM.BoObjectTypes.oPurchaseDeliveryNotes);
                if (lstGR.Count > 0)
                {
                    foreach (mdGoodsReceipt mdGR in lstGR) // Recorrem tota la llista
                    {
                        empID = mdGR.empID;
                       
                        CardCode = mdGR.CardCode;

                        oPurchaseDelivery.CardCode = mdGR.CardCode;
                        oPurchaseDelivery.DocDueDate = DateTime.Now;

                        /***** DEBUG *****/
                        Resposta += $"CardCode: {mdGR.CardCode}\n\r";
                        Resposta += $"Data: {DateTime.Now}\n\r";

                        if (nLinia > 0)
                            oPurchaseDelivery.Lines.Add();

                        /***** DEBUG *****/
                        Resposta += $"DocEntry: {mdGR.DocEntry}\n\r";
                        Resposta += $"LineNum: {mdGR.LineNum}\n\r";
                        Resposta += $"ItemCode: {mdGR.ItemCode}\n\r";
                        Resposta += $"Description: {mdGR.Dscription}\n\r";
                        Resposta += $"Quantity: {mdGR.RealQuantity}\n\r";
                        Resposta += $"Order: {nLinia}\n\r";
                        Resposta += $"Linia nou document: {oPurchaseDelivery.Lines.LineNum}\n\r";

                        // generem linies
                        oPurchaseDelivery.Lines.BaseType = (int)SAPbobsCOM.BoObjectTypes.oPurchaseOrders;
                        oPurchaseDelivery.Lines.BaseEntry = mdGR.DocEntry; //El numero de la comanda base
                        oPurchaseDelivery.Lines.BaseLine = mdGR.LineNum; //el numero de linia de la comanda
                        //oPurchaseDelivery.Lines.ShipDate = DateTime.Now;
                        //oPurchaseDelivery.Lines.ItemCode = mdGR.ItemCode;
                        //oPurchaseDelivery.Lines.ItemDescription = mdGR.Dscription;
                        //oPurchaseDelivery.Lines.Quantity = mdGR.RealQuantity;

                        nLinia++;
                    }

                    RetVal = oPurchaseDelivery.Add();

                    if (RetVal != 0)
                    {
                        Resposta += $"Error: {oCompany.GetLastErrorDescription()}\n\r";
                        ErrorsVaris = true;
                    }
                    else
                    {
                        Resposta = $"Added: {oCompany.GetNewObjectKey()}\n\r";
                        //updateTemporalGoodsReceipt(lstDocEntry, empID);
                    }
                }

                if (oCompany.InTransaction)
                {
                    if (ErrorsVaris == false)
                    {
                        oCompany.EndTransaction(BoWfTransOpt.wf_Commit);
                    }
                    else
                    {
                        oCompany.EndTransaction(BoWfTransOpt.wf_RollBack);
                    }
                }
            }
            return Resposta;
        }

        public string GetBPWithOpenOrders(string Warehouse)
        {
            try
            {
                string query = Funcions.GetQuery("GetBusinessPartnersWithOpenPurcharseOrders.sql");

                string[] parametres = {
                $"@WhsCode:{Warehouse}"
                };

                var dt = DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, query, parametres);

                var result = JsonConvert.SerializeObject(dt);

                return result;

            }
            catch (Exception e)
            {
                return e.Message;
            }

        }

        public string GetOpenOrdersFromBP(string Warehouse, string CardCode)
        {
            try
            {
                string query = Funcions.GetQuery("GetOpenPurcharseOrdersFromBP.sql");

                string[] parametres = {
                $"@WhsCode:{Warehouse}",
                $"@CardCode:{CardCode}"
                };

                var dt = DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, query, parametres);

                var result = JsonConvert.SerializeObject(dt);

                return result;

            }
            catch (Exception e)
            {
                return e.Message;
            }
            return null; 
        }

        public string GetOpenOrderFromBP(string Warehouse, string CardCode, int DocNum, int empID)
        {

            try
            {
                string query = Funcions.GetQuery("GetOpenPurcharseOrderFromBP.sql");

                string[] parametres = {
                $"@WhsCode:{Warehouse}",
                $"@CardCode:{CardCode}",
                $"@DocNum:{DocNum}",
                $"@empID:{empID}"
                };

                var dt = DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, query, parametres);

                var result = JsonConvert.SerializeObject(dt);

                return result;

            }
            catch (Exception e)
            {
                return e.Message;
            }
            return null;
        }

        public string GetTemporalGoodReceipt(string empID)
        {

            try
            {
                string query = Funcions.GetQuery("GetTemporalGoodsReceipt.sql");

                string[] parametres = {
                $"@empID:{empID}"
                };

                var dt = DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, query, parametres);

                var result = JsonConvert.SerializeObject(dt);

                return result;

            }
            catch (Exception e)
            {
                return e.Message;
            }
            return null;
        }

        public string updateTemporalGoodsReceipt(string DocEntry, int empID=0)
        {
            Dades.SetupDades();
            
            try
            {
                
                if (empID != 0)
                {
                    if (DocEntry != null)
                    {
                        
                        string sql = $"EXEC [dbo].[XNUPDATETMPGOODRECEIPT] @empID, @DocEntry";

                        List<SqlParameter> SqlParams = new List<SqlParameter>();
                        SqlParameter p = new SqlParameter();

                        p = new SqlParameter(); p.ParameterName = "@DocEntry"; p.SqlDbType = System.Data.SqlDbType.Int; p.SqlValue = DocEntry; SqlParams.Add(p);
                        p = new SqlParameter(); p.ParameterName = "@EmpID"; p.SqlDbType = System.Data.SqlDbType.Int; p.SqlValue = empID; SqlParams.Add(p);

                        string result = DataAccess.ExecuteQuery(Dades.ConnectionStringDOMENJO, sql, SqlParams.ToArray());

                        return $"OK#{result}#{Dades.ConnectionStringDOMENJO}";
                        
                        
                    } else
                    {
                        return $"ERROR#invalid DocEntry#";
                    }
                } else
                {
                    return $"ERROR#invalid empID#{empID}";
                }
            }
            catch (Exception e)
            {
                return $"ERROR#{e.Message}#{empID}";
            }

            return null;
        }
    }
}
