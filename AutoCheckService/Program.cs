// See https://aka.ms/new-console-template for more information

using SVNShareLib;

string BaseURL = "http://10.10.99.10:8101/";
string GetAndUploadProductionResultDataURL = "api/DB/GetAndUploadProductionResultData";

HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(BaseURL, 1000);
BODataProcessResult bODataProcessResult = new BODataProcessResult();
var result = await httpClientHelper.PostRequest(GetAndUploadProductionResultDataURL, bODataProcessResult, new CancellationToken(false));
Console.WriteLine("[Time]: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss") + " [Status]: " + result.OK.ToString() + " [Message]: " + result.Message);
