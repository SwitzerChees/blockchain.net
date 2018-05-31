using Blockchain.NET.Blockchain.Network;
using Blockchain.NET.Core;
using Blockchain.NET.Core.Helpers;
using Blockchain.NET.Core.Helpers.Calculations;
using Blockchain.NET.Core.Helpers.Cryptography;
using Blockchain.NET.Core.Mining;
using Blockchain.NET.Core.Store;
using Blockchain.NET.Core.Wallet;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Blockchain.NET.Blockchain
{
    public class BlockChain
    {
        public int DifficultyCorrectureInterval { get; private set; } = 512;
        public int DifficultyTimeTarget { get; private set; } = 30;
        public int NextBlockHeight
        {
            get
            {
                if (_nextBlock == null)
                {
                    _nextBlock = LastBlock();
                }
                return _nextBlock == null ? 1 : _nextBlock.Height;
            }
        }

        public Block NextBlock
        {
            get
            {
                return _nextBlock;
            }
        }

        public List<Transaction> MemPool
        {
            get
            {
                return _memPool;
            }
            set
            {
                _memPool = value;
            }
        }

        public Wallet Wallet { get; set; }

        public long MiningReward { get; set; } = 1000;

        public bool IsMining { get { return _isMining; } }

        public ActualInformation ActualInformation { get; set; } = new ActualInformation();

        private bool _isMining;
        private List<Transaction> _memPool;
        private NetworkSynchronizer _networkSynchronizer;
        private Block _nextBlock;

        public BlockChain(Wallet wallet)
        {
            Wallet = wallet;
            _memPool = new List<Transaction>();
            BlockchainDbContext.InitializeMigrations();
            _networkSynchronizer = new NetworkSynchronizer(this);
        }

        #region MINING

        public void StartMining()
        {
            if (!_isMining)
            {
                _isMining = true;
                new Thread(miningBlocks).Start();
            }
        }

        public void StopMining()
        {
            _isMining = false;
            if (_nextBlock != null)
                _nextBlock.IsMining = _isMining;
        }

        private void miningBlocks()
        {
            while (_isMining)
            {
                if (_networkSynchronizer.IsSynced)
                {
                    var lastBlock = LastBlock();

                    lock (_memPool)
                    {
                        var miningAddress = Wallet.NewAddress();
                        using (BlockchainDbContext db = new BlockchainDbContext())
                        {
                            var mempoolHashes = _memPool.Select(m => m.GenerateHash());
                            var existingTransactionsInBlockchain = db.Transactions.Where(t => mempoolHashes.Contains(t.Hash)).ToList();
                            existingTransactionsInBlockchain.ForEach(t => _memPool.Remove(_memPool.First(m => m.Hash == t.Hash)));
                            var transactionsInBlock = _memPool.OrderByDescending(t => t.TransactionFee).Take(100).ToList();
                            transactionsInBlock.Insert(0, new Transaction(null, Wallet, new[] { new Output(miningAddress.Key, MiningReward) }.ToList()));
                            _nextBlock = lastBlock == null ? new Block(1, null, transactionsInBlock) : new Block(lastBlock.Height + 1, HashHelper.ByteArrayToHexString(lastBlock.GenerateHash()), transactionsInBlock);
                        }
                    }
                    _nextBlock.MineBlock(CalculateDifficulty(_nextBlock), ActualInformation);
                    if (AddBlock(_nextBlock))
                        BlockchainConsole.WriteLine($"MINED BLOCK: {_nextBlock}", ConsoleEventType.MINEDBLOCK);
                    else
                        BlockchainConsole.WriteLine($"MINING FAILED: {_nextBlock}", ConsoleEventType.MININGFAILED);
                }
                else
                    Task.Delay(500);
            }
            ActualInformation.LiveMiningOutput = string.Empty;
        }

        private double CalculateDifficulty(Block block = null)
        {
            if (block == null)
                block = LastBlock();
            if (block == null || block.Height == 1)
                return 164.5;
            else
            {
                if (block != null && block.Height > 1 && block.Height % DifficultyCorrectureInterval == 1)
                {
                    var lowBlock = GetBlock(block.Height - DifficultyCorrectureInterval);
                    if (lowBlock == null)
                        return 0;
                    var lowTime = lowBlock.TimeStamp;
                    var lastBlock = GetBlock(block.Height - 1);
                    if (lastBlock != null)
                    {
                        var highTime = lastBlock.TimeStamp;

                        var valueToCorrect = ((highTime - lowTime).TotalSeconds / DifficultyCorrectureInterval) / DifficultyTimeTarget;

                        if (valueToCorrect > 1.02)
                            valueToCorrect = 1.02;
                        if (valueToCorrect < 0.98)
                            valueToCorrect = 0.98;

                        return lastBlock.Difficulty * valueToCorrect;
                    }
                    else
                        return 0;
                }
                var foundBlock = GetBlock(block.Height - 1);
                if (foundBlock != null)
                    return foundBlock.Difficulty;
                return 0;
            }
        }

        #endregion

        #region Syncronizing

        public void StartSyncronizing()
        {
            if (!_networkSynchronizer.IsSyncing)
            {
                _networkSynchronizer.Start();
            }
        }

        public void StopSyncronizing()
        {
            _networkSynchronizer.Stop();
        }

        #endregion

        #region MODIFICATION

        public bool PushBlock(Block block)
        {
            if (AddBlock(block))
            {
                if (_nextBlock != null)
                    _nextBlock.StopMining();
                return true;
            }
            return false;
        }

        public void AddBlocks(List<Block> blocks)
        {
            var start = DateTime.Now;
            using (BlockchainDbContext db = new BlockchainDbContext())
            {
                db.ChangeTracker.AutoDetectChangesEnabled = false;
                db.Blocks.AddRange(blocks);
                db.SaveChangesAsync();
            }
            var ts = DateTime.Now - start;
        }

        public bool AddBlock(Block block)
        {
            if (block == null)
                return false;
            //Check if Proof of Work is correct
            if (block.Difficulty > BigInteger.Log(new BigInteger(block.GenerateHash().Concat(new byte[] { 0, 0 }).ToArray())))
            {
                //Check if difficulty correct
                if (block.Difficulty == CalculateDifficulty(block))
                {
                    //Check if MerkleTree from transactions is correct
                    if (block.MerkleTreeHash == block.CreateMerkleTreeHash())
                    {
                        //Check if only one Coinbase transaction
                        if (block.Transactions.Count(t => t.Inputs == null || (t.Inputs != null && t.Inputs.Count == 0)) == 1)
                        {
                            //Check if first transaction is coinbase and the output of the transaction is mining reward and transaction fee not more not les
                            if (block.Transactions.Count > 0 && block.Transactions.First().Outputs.Count == 1 && block.Transactions.First().Outputs.First().Amount == (block.Transactions.Select(t => t.TransactionFee).Sum() + MiningReward))
                            {
                                //Check signatures and validity of transactions
                                bool transactionsValid = true;
                                if (block.Transactions != null)
                                {
                                    var inputAddresses = new List<string>();
                                    foreach (var actTransaction in block.Transactions)
                                    {
                                        if (actTransaction.Inputs != null)
                                            inputAddresses.AddRange(actTransaction.Inputs.Select(i => i.Key));
                                    }
                                    foreach (var transaction in block.Transactions.Skip(1))
                                    {
                                        if (!transaction.Verify())
                                        {
                                            transactionsValid = false;
                                            break;
                                        }
                                        else if (inputAddresses.Any(i => transaction.Outputs.Select(ip => ip.Key).Contains(i)))
                                        {
                                            transactionsValid = false;
                                            break;
                                        }
                                    }
                                }
                                if (transactionsValid)
                                {
                                    using (BlockchainDbContext db = new BlockchainDbContext())
                                    {
                                        var existingBlock = db.Blocks.FirstOrDefault(b => b.Height == block.Height);
                                        if (existingBlock == null)
                                        {
                                            var findLastBlock = block.Height == 1 ? null : db.Blocks.FirstOrDefault(b => b.Height == block.Height - 1);
                                            if (block.Height == 1 || (findLastBlock != null && HashHelper.ByteArrayToHexString(findLastBlock.GenerateHash()) == block.PreviousHash))
                                            {
                                                db.Blocks.Add(block);
                                                db.SaveChanges();
                                                foreach (var transactionToDelete in block.Transactions)
                                                {
                                                    var foundTransaction = _memPool.FirstOrDefault(t => t.GenerateHash() == transactionToDelete.GenerateHash());
                                                    if (foundTransaction != null)
                                                        _memPool.Remove(foundTransaction);
                                                }
                                                block.StopMining();
                                                _networkSynchronizer.BroadcastBlock(block);
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }

        public void AddTransaction(Transaction transaction)
        {
            if (transaction.Verify())
            {
                // Check if 
                if (transaction.Outputs != null)
                {
                    // If Coinbase transaction no validity
                    if (transaction.Inputs == null)
                    {
                        _memPool.Add(transaction);
                    }
                    else
                    {
                        decimal balance = BalanceHelper.GetBalanceOfAddresses(transaction.Inputs.Select(i => i.Key).ToArray());
                        // Check if enough balance on inputs
                        if (balance >= (transaction.Outputs.Select(o => o.Amount).Sum() + transaction.TransactionFee))
                        {
                            bool everUsedAsInput = BalanceHelper.EverUsedAsInput(transaction.Inputs.Select(i => i.Key).ToArray());
                            // Check if ever used as input before
                            if (!everUsedAsInput)
                            {
                                if (!_memPool.Select(mp => mp.GenerateHash()).Any(t => t == transaction.GenerateHash()))
                                {
                                    _memPool.Add(transaction);
                                    _networkSynchronizer.BroadcastTransaction(transaction);
                                }
                            }
                            else
                            {
                                throw new Exception("Eine der Adressen wurde bereits als Input verwendet!");
                            }
                        }
                        else
                        {
                            throw new Exception("Nicht genug Guthaben!");
                        }
                    }
                }
            }
            else
            {
                throw new Exception("Nicht valide Transaktion!");
            }
        }

        #endregion

        #region VALIDITY / READ
        public Block LastBlock()
        {
            using (BlockchainDbContext db = new BlockchainDbContext())
            {
                return db.Blocks.OrderBy(b => b.Height).LastOrDefault();
            }
        }

        public Block IsBlockValid(Block block)
        {
            if (block == null)
                return block;
            using (BlockchainDbContext db = new BlockchainDbContext())
            {
                var chainUnderBlock = db.Blocks.Include("Transactions.Outputs").Include("Transactions.Inputs").Where(b => b.Height < block.Height).OrderByDescending(b => b.Height).ToList();

                if (chainUnderBlock.Count() > 0)
                {
                    var baseHash = block.PreviousHash;
                    foreach (var actBlock in chainUnderBlock)
                    {
                        actBlock.MerkleTreeHash = actBlock.CreateMerkleTreeHash();
                        var hashFromBlock = HashHelper.ByteArrayToHexString(actBlock.GenerateHash());

                        if (hashFromBlock != baseHash)
                            return actBlock;
                        baseHash = actBlock.PreviousHash;
                    }
                }
            }
            return null;
        }

        public Block GetBlock(int height, bool includeTransactions = false)
        {
            using (BlockchainDbContext db = new BlockchainDbContext())
            {
                if (includeTransactions)
                {
                    return db.Blocks.Include("Transactions.Outputs").Include("Transactions.Inputs").FirstOrDefault(b => b.Height == height);
                }
                return db.Blocks.FirstOrDefault(b => b.Height == height);
            }
        }

        public List<Block> GetBlocks(List<int> blockHeights, bool includeTransactions = false)
        {
            using (BlockchainDbContext db = new BlockchainDbContext())
            {
                if (includeTransactions)
                {
                    return db.Blocks.Include("Transactions.Outputs").Include("Transactions.Inputs").Where(b => blockHeights.Contains(b.Height)).ToList();
                }
                return db.Blocks.Where(b => blockHeights.Contains(b.Height)).ToList();
            }
        }

        public List<Block> GetBlocks(int blockHeight, int blockCount = 250)
        {
            using (BlockchainDbContext db = new BlockchainDbContext())
            {
                return db.Blocks.OrderByDescending(b => b.Height).Where(b => b.Height <= blockHeight).Take(blockCount).ToList();
            }
        }

        public List<Transaction> GetTransactions(int transactionHeight, int blockCount = 250)
        {
            using (BlockchainDbContext db = new BlockchainDbContext())
            {
                return db.Transactions.Include(t => t.Inputs).Include(t => t.Outputs).OrderByDescending(b => b.Id).Where(b => b.Id <= transactionHeight).Take(blockCount).ToList();
            }
        }

        public bool IsChainValid()
        {
            var lastBlock = LastBlock();

            return IsBlockValid(lastBlock) == null ? true : false;
        }

        public string BlockchainHash(int blockHeight)
        {
            using (BlockchainDbContext db = new BlockchainDbContext())
            {
                var blockHashes = db.Blocks.Where(b => b.Height <= blockHeight).Select(b => b.PreviousHash).ToList();
                return HashHelper.Sha256(string.Join("", blockHashes));
            }
        }

        public List<string> BlockHashes()
        {
            using (BlockchainDbContext db = new BlockchainDbContext())
            {
                var blockHashes = db.Blocks.Select(b => b.PreviousHash).ToList();
                return blockHashes;
            }
        }

        #endregion

        #region Balance
        public int TransactionsCount()
        {
            using (BlockchainDbContext db = new BlockchainDbContext())
            {
                return db.Transactions.Count();
            }
        }

        public Transaction LastTransaction()
        {
            using (BlockchainDbContext db = new BlockchainDbContext())
            {
                return db.Transactions.Include(b => b.Block).Include(b => b.Inputs).Include(b => b.Outputs).OrderBy(b => b.BlockHeight).ThenBy(b => b.Id).LastOrDefault();
            }
        }
        #endregion
    }
}
