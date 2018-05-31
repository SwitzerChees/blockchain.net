using Blockchain.NET.Blockchain.Network;
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

        [HttpGet("[action]/{transactionHeight}")]
        public JsonResult GetTransactions(int transactionHeight)
        {
            var transactions = Program.BlockChain.GetTransactions(transactionHeight);
            var transactionViews = new List<TransactionViewModel>();
            foreach (var transaction in transactions)
            {
                var newTransViewModel = new TransactionViewModel()
                {
                    Id = transaction.Id,
                    Amount = transaction.Outputs.Select(o => o.Amount).Sum(),
                    IsCoinbase = transaction.Inputs.Count == 0,
                };
                if (transaction.Outputs.Count > 0)
                {
                    newTransViewModel.Receiver = transaction.Outputs.First().Key;
                }
                if (!newTransViewModel.IsCoinbase)
                {
                    var lastAmount = transaction.Outputs.LastOrDefault();
                    if (lastAmount != null)
                    {
                        newTransViewModel.Amount -= lastAmount.Amount;
                    }
                }
                transactionViews.Add(newTransViewModel);
            }
            return Json(transactionViews, new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
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
            if (lastBlock == null)
                lastBlock = new Block() { Nonce = 0, TimeStamp = DateTime.Now };
            var transactionCount = Program.BlockChain.TransactionsCount();
            var lastTransaction = Program.BlockChain.LastTransaction();
            if (lastTransaction == null)
                lastTransaction = new Transaction() {
                    Block = lastBlock,
                    Outputs = new List<Output>()
                };
            return Ok($"{lastBlock.Height}: Nonce = {lastBlock.Nonce},{transactionCount},{lastTransaction.Block.TimeStamp}: Inputs = {(lastTransaction.Inputs != null ? lastTransaction.Inputs.Count : 0)} / Outputs = {lastTransaction.Outputs.Count}");
        }

        [HttpPost("[action]")]
        public IActionResult SetNodeUrl([FromBody]SetNodeModel model)
        {
            NetworkSynchronizer.NodeUrl = model.NodeUrl;
            return Ok();
        }
    }


    public class SetNodeModel
    {
        public string NodeUrl { get; set; }
    }

    public class TransactionViewModel
    {
        public long Id { get; set; }

        public string Receiver { get; set; }

        public decimal Amount { get; set; }

        public bool IsCoinbase { get; set; }
    }
}
