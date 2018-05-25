using Blockchain.NET.Core.Mining;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Blockchain.NET.Node.V1.Dashboard.Controllers
{
    [Produces("application/json")]
    [ApiVersion("1.0")]
    [Route("api/v{apiVersion:apiVersion}/[controller]")]
    public class DashboardController : Controller
    {
        [HttpGet("[action]")]
        public IActionResult Health()
        {
            return Ok();
        }

        [HttpGet("[action]/{blockHeight}")]
        public JsonResult GetBlocks(int blockHeight)
        {
            return Json(Program.BlockChain.GetBlocks(blockHeight), new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        }
    }
}
