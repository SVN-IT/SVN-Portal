using OdooRpc.CoreCLR.Client;
using OdooRpc.CoreCLR.Client.Models;
using OdooRpc.CoreCLR.Client.Models.Parameters;

namespace ViidooDBServiceAPI.Services
{
    public class SVNOdooRpcClient : OdooRpcClient
    {
        public SVNOdooRpcClient(OdooConnectionInfo connectionInfo) : base(connectionInfo)
        {

        }

        public Task<T> GetSVNAll<T>(string model, OdooDomainFilter domainFilter, OdooFieldParameters fieldParameters, OdooPaginationParameters pagParameters)
        {
            return Get<T>(new OdooSearchParameters(model, domainFilter), fieldParameters, pagParameters);
        }
    }
}
