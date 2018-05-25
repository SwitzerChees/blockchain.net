using Blockchain.NET.Core.Helpers;
using Blockchain.NET.Core.Helpers.Cryptography;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Blockchain.NET.Core.Mining
{
    public class Block
    {
        [Key]
        public int Height { get; set; }

        public string PreviousHash { get; set; }

        public DateTime TimeStamp { get; set; }

        public long Nonce { get; set; }

        public double Difficulty { get; set; }

        public string MerkleTreeHash { get; set; }

        public virtual ICollection<Transaction> Transactions { get; set; }

        private bool _isMining;

        [JsonIgnore]
        [NotMapped]
        public bool IsMining
        {
            get { return _isMining; }
            set { _isMining = value; }
        }

        public Block() { }

        public Block(int height, string previousHash, List<Transaction> transactions)
        {
            Height = height;
            TimeStamp = DateTime.Now;
            PreviousHash = previousHash;
            Transactions = transactions;
            MerkleTreeHash = CreateMerkleTreeHash();
            var genesisTransaction = Transactions.FirstOrDefault(t => t.Inputs == null);
            if(genesisTransaction != null)
            {
                if (genesisTransaction.Outputs.Count > 0)
                    genesisTransaction.Outputs.First().Amount += transactions.Where(t => t.Inputs != null).Select(t => t.TransactionFee).Sum();
            }
        }

        public string CreateMerkleTreeHash()
        {
            if (Transactions != null)
                if (Transactions.Count > 0)
                {
                    var merkleTreeList = Transactions.Select(t => t.GenerateHash(true)).ToList();
                    while (merkleTreeList.Count > 1)
                    {
                        merkleTreeList.Add(HashHelper.Sha256(merkleTreeList[0] + merkleTreeList[1]));
                        merkleTreeList.RemoveRange(0, 2);
                    }
                    return merkleTreeList[0];
                }
            return string.Empty;
        }

        public void MineBlock(double difficulty, ActualInformation actualInformation)
        {
            if (!_isMining)
            {
                Difficulty = difficulty;
                _isMining = true;

                var totalHashesCounter = 0;
                DateTime startTime = DateTime.Now;

                Task[] miningTasks = new Task[1];
                //#if DEBUG
                //                Task[] miningTasks = new Task[1];
                //#else
                //                Task[] miningTasks = new Task[Environment.ProcessorCount];
                //#endif
                var unsigned = new byte[] { 0, 0 };
                for (int i = 0; i < miningTasks.Length; i++)
                {
                    var startNonce = i;
                    miningTasks[i] = Task.Factory.StartNew(() =>
                    {
                        var taskHash = GenerateHash(startNonce);
                        while (difficulty < BigInteger.Log(new BigInteger(taskHash.Concat(unsigned).ToArray())))
                        {
                            if (!_isMining)
                            {
                                taskHash = null;
                                break;
                            }
                            startNonce = startNonce + miningTasks.Length;
                            taskHash = GenerateHash(startNonce);
                            totalHashesCounter++;
                            if (totalHashesCounter % 200000 == 0)
                            {
                                var currentElapsedTime = DateTime.Now - startTime;
                                actualInformation.LiveMiningOutput = $"MINING BLOCK: {Height}, ELAPSED TIME: {currentElapsedTime.TotalSeconds} Seconds, DIFFICULTY: {difficulty}, HASH RATE: {Convert.ToInt64(totalHashesCounter / currentElapsedTime.TotalSeconds)}/Seconds";
                                BlockchainConsole.WriteLive(actualInformation.LiveMiningOutput);
                            }
                        }
                        if (taskHash != null)
                        {
                            Nonce = startNonce;
                            _isMining = false;
                        }
                    });
                }
                Task.WaitAll(miningTasks);
                var elapsedTime = DateTime.Now - startTime;
                BlockchainConsole.WriteLive(string.Empty);
                BlockchainConsole.WriteLine($"ELAPSED TIME: {elapsedTime.TotalSeconds} Seconds, DIFFICULTY: {difficulty}, HASH RATE: {Convert.ToInt64(totalHashesCounter / elapsedTime.TotalSeconds)}/Seconds                              ", ConsoleEventType.Elapsed);
            }
        }

        public byte[] GenerateHash(long nonce = -1)
        {
            if (nonce < 0)
                return HashHelper.Sha256Bytes(Height + PreviousHash + TimeStamp.Ticks + Nonce + Difficulty + MerkleTreeHash);
            else
                return HashHelper.Sha256Bytes(Height + PreviousHash + TimeStamp.Ticks + nonce + Difficulty + MerkleTreeHash);
        }

        public void StopMining()
        {
            _isMining = false;
        }

        public override string ToString()
        {
            return $"BlockNumber: {Height}, Hash: {HashHelper.ByteArrayToHexString(GenerateHash())}";
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }); 
        }
    }
}
