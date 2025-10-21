// See https://aka.ms/new-console-template for more information

using Org.BouncyCastle.Asn1.Crmf;
using SVNShareLib;
using SVNShareLib.Request;

string BaseURL = "https://localhost:7272/"; //http://10.10.99.10:8101/ https://localhost:7272/
string GetAndUploadProductionResultDataURL = "api/YeelightService/SetColor";
string GetDataFromViindooV1URL = "api/DB/GetLotByMODone"; //api/DB/GetDataFromViindooV1
string GetDataFromViindooV3URL = "api/DB/GetDataFromViindooV3";

//ViindooDataRequest dataRequest = new ViindooDataRequest()
//{
//    TableName = "mrp.production",
//    Domain = "state,=,done",
//    Fields = "id,product_id,product_uom_id,lot_producing_id,bom_id,name,priority,origin,state,reservation_state,consumption,product_qty,qty_producing,date_planned_start,date_planned_finished,date_deadline,date_start,date_finished,product_uom_qty,x_Svn_customer_SN,finished_move_line_ids",
//    Limit = 0,
//    Order = "date_finished desc"
//};

ProductDataRequest dataRequest = new ProductDataRequest()
{
    product_id = 236,
    count = 10,
    seriNumber = "1234567890"
};

HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(BaseURL, 1000);
BODataProcessResult bODataProcessResult = new BODataProcessResult();
var result = await httpClientHelper.PostRequest(GetDataFromViindooV1URL, dataRequest, new CancellationToken(false));
Console.WriteLine("[Time]: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss") + " [Status]: " + result.OK.ToString() + " [Message]: " + result.Message);
