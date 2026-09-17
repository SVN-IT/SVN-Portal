using Microsoft.AspNetCore.Mvc;
using SVNShareLib;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using System.Data.SqlClient;

namespace ViidooDBServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NetsuiteController : Controller
    {
        SVNDBConfig svnDBConfig;
        public NetsuiteController(SVNDBConfig svnDBConfig)
        {
            this.svnDBConfig = svnDBConfig;
        }

        [HttpPost("ReceiveWorkOrderBatch")]
        public async Task<IActionResult> ReceiveWorkOrderBatch([FromBody] List<SVN_OracleWorkOrderLogUI> dtoList)
        {
            var dataPortal = new SVN_OracleWorkOrderLogDataPortal(svnDBConfig.ConnectionString);
            var result = await dataPortal.SynchWOLogs(dtoList);
            string statusMessage = result > 0 ? "Success" : "Failed";
            return Ok(new { status = statusMessage, count = result });
        }
    }
}
