using Microsoft.AspNetCore.Mvc;
using SVNShareLib;
using SVNShareLib.Request;
using ViidooDBServiceAPI.Services;

namespace ViidooDBServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class YeelightServiceController : Controller
    {
        [Route("SetPower")]
        [HttpPost]
        public BODataProcessResult SetPower(SetLightRequest setLightRequest)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                YeelightService service = new YeelightService(setLightRequest.IP, setLightRequest.Port);
                service.SetPower(setLightRequest.Power);
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
        public BODataProcessResult SetColor(SetLightRequest setLightRequest)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                YeelightService service = new YeelightService(setLightRequest.IP, setLightRequest.Port);
                service.SetColor(setLightRequest.Color);
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
        public BODataProcessResult SetBrightness(SetLightRequest setLightRequest)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                YeelightService service = new YeelightService(setLightRequest.IP, setLightRequest.Port);
                service.SetBrightness(setLightRequest.Brightness);
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
