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

        [HttpGet("[action]")]
        public IActionResult ToggleMiner()
        {
            if (Program.BlockChain.IsMining)
            {
                Program.BlockChain.StopMining();
            }
            else
            {
                Program.BlockChain.StartMining();
            }
            return Json(Program.BlockChain.IsMining);
        }

        [HttpGet("[action]")]
        public IActionResult MinerState()
        {
            return Json(Program.BlockChain.IsMining);
        }

        [HttpGet("[action]")]
        public IActionResult ActualInformation()
        {
            return Ok(Program.BlockChain.ActualInformation.LiveMiningOutput);
        }

        [HttpGet("[action]")]
        public IActionResult GeneralInformation()
        {
            var lastBlock = Program.BlockChain.LastBlock();
            var transactionCount = Program.BlockChain.TransactionsCount();
            var lastTransaction = Program.BlockChain.LastTransaction();
            return Ok($"{lastBlock.Height}: Nonce = {lastBlock.Nonce},{transactionCount},{lastTransaction.Block.TimeStamp}: Inputs = {(lastTransaction.Inputs != null ? lastTransaction.Inputs.Count : 0)} / Outputs = {lastTransaction.Outputs.Count}");
        }
    }
}
