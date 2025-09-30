using Microsoft.AspNetCore.Mvc;
using SVNShareLib;
using SVNShareLib.Request;
using System.Drawing;
using System.Threading.Tasks;
using ViidooDBServiceAPI.Services;

namespace ViidooDBServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class YeelightServiceController : Controller
    {
        [Route("SetPower")]
        [HttpPost]
        public async Task<BODataProcessResult> SetPower(SetLightRequest setLightRequest)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                YeelightService service = new YeelightService(setLightRequest.IP, setLightRequest.Port);
                await service.SetPower(setLightRequest.Power);
                processResult.OK = true;
                processResult.Message = "Set power success";
            }
            catch (Exception ex) 
            { 
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        [Route("SetColor")]
        [HttpPost]
        public async Task<BODataProcessResult> SetColor(SetLightRequest setLightRequest)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                YeelightService service = new YeelightService(setLightRequest.IP, setLightRequest.Port);
                var color = Color.FromName(setLightRequest.Color);
                await service.SetColor(color);
                processResult.OK = true;
                processResult.Message = "Set power success";
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        [Route("SetBrightness")]
        [HttpPost]
        public async Task<BODataProcessResult> SetBrightness(SetLightRequest setLightRequest)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                YeelightService service = new YeelightService(setLightRequest.IP, setLightRequest.Port);
                await service.SetBrightness(setLightRequest.Brightness);
                processResult.OK = true;
                processResult.Message = "Set power success";
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }
    }
}
