using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;
using System.Xml;
using FliggyWebApi.Dto;
using FliggyWebApi.Impl;

namespace FliggyWebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FliggyController : ControllerBase
    {
        private readonly IFliggyDomain _fliggyDomain;

        public FliggyController(IFliggyDomain fliggyDomain)
        {
            _fliggyDomain = fliggyDomain;
        }

        [HttpPost("process")]
        [Consumes("application/xml")]
        [Produces("application/xml")]
        public async Task<IActionResult> ProcessRequest()
        {
            try
            {
                using (var reader = new StreamReader(Request.Body))
                {
                    string body = await reader.ReadToEndAsync();

                    var res = await _fliggyDomain.Process(body);

                    // 返回 XML 响应
                    return Content(res, "application/xml");
                }
            }
            catch (Exception ex)
            {
                var errorResponse = new FliggyResult
                {
                    Message = "服务出现异常",
                    ResultCode = -1
                };

                return StatusCode(500, errorResponse);
            }
        }
    }
}