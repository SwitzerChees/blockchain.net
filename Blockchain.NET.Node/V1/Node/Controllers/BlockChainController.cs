using Blockchain.NET.Blockchain.Network;
using Blockchain.NET.Core.Mining;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Blockchain.NET.Node.V1.Node.Controllers
{
    [Produces("application/json")]
    [ApiVersion("1.0")]
    [Route("api/v{apiVersion:apiVersion}/[controller]")]
    public class BlockChainController : Controller
    {
        [HttpGet("[action]")]
        public IActionResult Health()
        {
            return Ok();
        }

        [HttpGet("[action]")]
        public JsonResult LastBlock()
        {
            return Json(Program.BlockChain.LastBlock());
        }

        [HttpGet("[action]")]
        public IActionResult LastBlockHeight()
        {
            return Content((Program.BlockChain.NextBlockHeight - 1).ToString());
        }

        [HttpGet("[action]")]
        public JsonResult MemPoolHashes()
        {
            return Json(Program.BlockChain.MemPool.Select(m => m.GenerateHash()).ToList());
        }

        [HttpGet("[action]")]
        public JsonResult BlockHashes()
        {
            return Json(Program.BlockChain.BlockHashes());
        }

        [HttpGet("[action]/{blockHeight}")]
        public IActionResult GetBlockchainHash(int blockHeight)
        {
            return Content(Program.BlockChain.BlockchainHash(blockHeight));
        }

        [HttpGet("[action]")]
        public JsonResult GetTransactions([FromBody]List<string> hashes)
        {
            return Json(Program.BlockChain.MemPool.Where(mp => hashes.Contains(mp.GenerateHash())).ToList(), new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        }

        [HttpGet("[action]/{blockHeight}")]
        public JsonResult GetBlock(int blockHeight)
        {
            return Json(Program.BlockChain.GetBlock(blockHeight, true), new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        }

        [HttpPost("[action]")]
        public JsonResult GetBlocks([FromBody]List<int> blockHeights)
        {
            return Json(Program.BlockChain.GetBlocks(blockHeights, true), new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        }

        [HttpPost("[action]")]
        public IActionResult PushBlock([FromBody]Block block)
        {
            if (Program.BlockChain.PushBlock(block))
                return Ok();
            return BadRequest();
        }

        [HttpPost("[action]")]
        public IActionResult PushTransaction([FromBody] Transaction transaction)
        {
            try
            {
                Program.BlockChain.AddTransaction(transaction);
                return Ok();
            }
            catch
            {
                return BadRequest();
            }
        }
    }
}
