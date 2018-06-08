using Blockchain.NET.Core.Helpers;
using Blockchain.NET.Core.Helpers.Calculations;
using Blockchain.NET.Core.Helpers.Cryptography;
using Blockchain.NET.Core.Mining;
using Blockchain.NET.Core.Store;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Blockchain.NET.Core.Wallet
{
    public class Wallet
    {
        public List<Address> Addresses = new List<Address>();

        public string PasswordSignature { get; set; }

        private string _password;

        private static string _walletName = "wallet.sec";
        private static string _rootPath = "Data";

        public Wallet(string password, string walletName = "")
        {
            _password = password;
            if (!string.IsNullOrEmpty(walletName))
                _walletName = walletName + ".sec";
        }

        public void ChangePassword(string newPassword)
        {
            _password = newPassword;
            PasswordSignature = HashHelper.Sha256(newPassword);
            Save();
        }

        public bool ValidSignature(string signature)
        {
            return PasswordSignature == signature;
        }

        public void Save()
        {
            if (!Directory.Exists(_rootPath))
                Directory.CreateDirectory(_rootPath);
            File.WriteAllBytes(Path.Combine(_rootPath, _walletName), AESHelper.Encrypt(SerializeHelper.Serialize(this), _password));
        }

        public static Wallet Load(string password)
        {
            if (!Directory.Exists(_rootPath))
                Directory.CreateDirectory(_rootPath);
            if (!File.Exists(Path.Combine(_rootPath, _walletName)))
            {
                new Wallet(password) { PasswordSignature = HashHelper.Sha256(password) }.Save();
            }
            var wallet = SerializeHelper.Deserialize<Wallet>(AESHelper.Decrypt(File.ReadAllBytes(Path.Combine(_rootPath, _walletName)), password));
            wallet._password = password;
            return wallet;
        }

        public Address NewAddress()
        {
            var newAddress = Address.New();
            Addresses.Add(newAddress);
            Save();
            return newAddress;
        }

        public Transaction CreateTransaction(string address, long amount, string message)
        {
            var balancesPerOutput = GetBalancesPerOutput();
            var aggregatedOutputs = new List<Output>();
            foreach (var balanceOutput in balancesPerOutput)
            {
                aggregatedOutputs.Add(balanceOutput);
                if (aggregatedOutputs.Select(o => o.Amount).Sum() >= amount)
                    break;
            }

            var newTransaction = new Transaction(aggregatedOutputs.Select(o => new Input(o.Key)).ToList(), this, new[] { new Output(address, amount) }.ToList(), data: Encoding.Unicode.GetBytes(message));

            return newTransaction;
        }

        public decimal GetBalance()
        {
            var walletAddresses = Addresses.Select(a => a.Key).ToArray();
            return BalanceHelper.GetBalanceOfAddresses(walletAddresses);
        }

        public List<Output> GetBalancesPerOutput()
        {
            var balancesPerAddress = new Dictionary<string, decimal>();
            var walletAddresses = Addresses.Select(a => a.Key).ToArray();

            using (BlockchainDbContext db = new BlockchainDbContext())
            {
                var usedOutputs = db.Outputs.Where(o => walletAddresses.Contains(o.Key)).ToList();
                var usedInputs = db.Inputs.Where(o => walletAddresses.Contains(o.Key)).Select(i => i.Key).ToList();
                var outputsWithBalance = new List<Output>();
                foreach (var output in usedOutputs)
                {
                    if (!usedInputs.Contains(output.Key))
                        outputsWithBalance.Add(output);
                }
                return outputsWithBalance;
            }
        }

        public List<TransactionView> GetTransactions(int blockHeight = 0)
        {
            var transactionViews = new List<TransactionView>();
            var walletAddresses = Addresses.Select(a => a.Key).ToArray();

            using (BlockchainDbContext db = new BlockchainDbContext())
            {
                var transactions = db.Transactions.Include(t => t.Inputs).Include(t => t.Outputs).Where(t => t.BlockHeight > blockHeight && (t.Inputs.Select(i => i.Key).Intersect(walletAddresses).Any() || t.Outputs.Select(i => i.Key).Intersect(walletAddresses).Any())).OrderByDescending(t => t.BlockHeight).ThenByDescending(t => t.Id).Take(10).ToList();

                foreach (var transaction in transactions)
                {
                    var isIncome = transaction.Inputs == null ? true : transaction.Inputs.Select(i => i.Key).All(o => !walletAddresses.Contains(o));
                    transactionViews.Add(new TransactionView()
                    {
                        Inputs = transaction.Inputs.Select(i => new IOView() { Key = i.Key, Amount = BalanceHelper.GetBalanceOfAddress(i.Key) }).ToArray(),
                        Message = transaction.Data == null ? string.Empty : Encoding.Unicode.GetString(transaction.Data),
                        BlockHeight = transaction.BlockHeight,
                        Outputs = transaction.Outputs.Select(o => new IOView() { Key = o.Key, Amount = o.Amount }).ToArray(),
                        Amount = transaction.Inputs == null || transaction.Inputs.Count == 0 ? transaction.Outputs.Select(o => o.Amount).Sum() : isIncome ? transaction.Outputs.Select(o => o.Amount).Sum() - transaction.Outputs.Last().Amount : - transaction.Outputs.Where(o => !walletAddresses.Contains(o.Key)).Select(o => o.Amount).Sum(),
                        IsIncome = isIncome
                    });
                }
            }
            return transactionViews.OrderBy(t => t.BlockHeight).ToList();
        }
    }
}
