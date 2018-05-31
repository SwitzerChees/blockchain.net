using Blockchain.NET.Blockchain.Network.Settings;
using Blockchain.NET.Core.Mining;
using Blockchain.NET.Core.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Blockchain.NET.Blockchain.Network
{
    public class NetworkSynchronizer
    {
        private readonly BlockChain _blockChain;

        private Thread _syncBlocksThread;
        private Thread _connectNodesThread;
        private Thread _transactionsThread;
        private Thread _syncBlockchainsThread;

        public bool IsSyncing { get; set; }
        private static Random rnd = new Random();

        public static string NodeUrl { get; set; }

        public List<NodeConnection> Connections
        {
            get { return _connections; }
            set { _connections = value; }
        }

        private bool _isSynced;
        public bool IsSynced
        {
            get { return !_connectionsEstablished ? false : _isSynced; }
            set
            {
                _isSynced = value;
                if (!_isSynced)
                {
                    if (_blockChain.NextBlock != null)
                        _blockChain.NextBlock.StopMining();
                }
            }
        }

        private bool _isBlockchainSynced;
        private bool _connectionsEstablished;

        private NodeList _nodeList;
        private List<NodeConnection> _connections;

        public NetworkSynchronizer(BlockChain blockChain)
        {
            _blockChain = blockChain;
            _nodeList = NodeList.Load();
        }

        public void Start()
        {
            if (!IsSyncing)
            {
                IsSyncing = true;
                if (_syncBlocksThread == null || _syncBlocksThread.ThreadState != ThreadState.Running)
                {
                    _syncBlocksThread = new Thread(syncBlocksThread);
                    _connectNodesThread = new Thread(connectNodesThread);
                    _transactionsThread = new Thread(syncTransactionsThread);
                    _syncBlockchainsThread = new Thread(syncBlockchainsThread);
                }
                _connections = new List<NodeConnection>();
                _syncBlocksThread.Start();
                _connectNodesThread.Start();
                _transactionsThread.Start();
                _syncBlockchainsThread.Start();
            }
        }

        public void Stop()
        {
            IsSyncing = false;
            _syncBlocksThread.Abort();
            _connectNodesThread.Abort();
            _transactionsThread.Abort();
            _syncBlockchainsThread.Abort();
        }

        private async void connectNodesThread()
        {
            while (true)
            {
                if (_connections.Count < 5 && !string.IsNullOrEmpty(NodeUrl))
                {
                    foreach (var node in _nodeList.Nodes.Where(n => !_connections.Select(c => c.NodeAddress).Contains(n.NodeAddress) && !NodeUrl.Contains(n.NodeAddress)))
                    {
                        if (node.LastConnectionAttempt.HasValue)
                        {
                            if (node.LastConnectionAttempt.Value < DateTime.Now.AddSeconds(30))
                            {
                                var newConnection = new NodeConnection(node.NodeAddress);
                                if (await newConnection.Health())
                                {
                                    _connections.Add(newConnection);
                                    node.LastConnectionAttempt = null;
                                    IsSynced = false;
                                }
                            }
                        }
                    }
                    _connectionsEstablished = true;
                }

                var lostConnections = new List<NodeConnection>();
                foreach (var connection in _connections)
                {
                    if (!await connection.Health())
                    {
                        lostConnections.Add(connection);
                    }
                }
                lostConnections.ForEach(lc => _connections.Remove(lc));

                for (int i = 0; i < 4; i++)
                {
                    await Task.Delay(1000);
                    if (!IsSyncing)
                        break;
                }
            }
        }

        private async void syncBlocksThread()
        {
            while (IsSyncing)
            {
                if (_isBlockchainSynced)
                {
                    var lastBlock = _blockChain.LastBlock();
                    var nextBlockHeight = lastBlock == null ? 1 : _blockChain.LastBlock().Height + 1;
                    var resultRemoteChainStates = new List<Tuple<NodeConnection, int>>();
                    foreach (var connection in _connections.ToList())
                    {
                        var lastBlockHeight = await connection.LastBlockHeight();
                        if (lastBlockHeight > nextBlockHeight)
                        {
                            resultRemoteChainStates.Add(new Tuple<NodeConnection, int>(connection, lastBlockHeight));
                            break;
                        }
                    }
                    if (resultRemoteChainStates.Count > 0)
                    {
                        IsSynced = false;
                        var from = nextBlockHeight;
                        var to = resultRemoteChainStates.Select(t => t.Item2).Max();
                        loadBlocks(resultRemoteChainStates, from, to);
                    }
                    else
                    {
                        IsSynced = true;
                        for (int i = 0; i < 8; i++)
                        {
                            await Task.Delay(1000);
                            if (!IsSyncing)
                                break;
                        }
                    }
                }
                else
                    await Task.Delay(500);
            }
        }

        private void loadBlocks(List<Tuple<NodeConnection, int>> resultRemoteChainStates, int from, int to)
        {
            var tasks = new List<Task<List<Block>>>();
            var requestCount = 0;
            while (true)
            {
                var connection = resultRemoteChainStates.Where(rc => rc.Item2 >= from).OrderBy(x => rnd.Next()).FirstOrDefault();
                var newTo = from + 200;
                if (newTo > to)
                    newTo = to;
                tasks.Add(connection.Item1.GetBlocks(Enumerable.Range(from, newTo).ToList()));
                requestCount++;
                from = newTo + 1;
                if (newTo >= to || requestCount >= _connections.Count)
                    break;
            }
            Task.WaitAll(tasks.ToArray());
            var blocksToSave = new List<Block>();
            foreach (var taskResult in tasks.Select(t => t.Result))
            {
                if (taskResult != null)
                {
                    blocksToSave.AddRange(taskResult);
                }
            }
            blocksToSave = blocksToSave.OrderBy(b => b.Height).ToList();
            _blockChain.AddBlocks(blocksToSave);
        }

        private async void syncTransactionsThread()
        {
            while (IsSyncing)
            {
                var localMempoolHashes = _blockChain.MemPool.Select(t => t.GenerateHash());
                var resultRemoteChainStates = new List<Tuple<NodeConnection, List<string>>>();
                foreach (var connection in _connections.ToList())
                {
                    var mempoolHashes = await connection.MempoolHashes();

                    if (mempoolHashes != null)
                    {
                        var notExistingTransactions = mempoolHashes.Where(mp => !localMempoolHashes.Any(lmp => lmp == mp)).ToList();

                        if (notExistingTransactions.Count > 0)
                        {
                            resultRemoteChainStates.Add(new Tuple<NodeConnection, List<string>>(connection, notExistingTransactions));
                            break;
                        }
                    }
                }
                loadTransactions(resultRemoteChainStates);
                for (int i = 0; i < 8; i++)
                {
                    await Task.Delay(1000);
                    if (!IsSyncing)
                        break;
                }
            }
        }

        private void loadTransactions(List<Tuple<NodeConnection, List<string>>> resultRemoteChainStates)
        {
            var tasks = new List<Task<List<Transaction>>>();
            foreach (var connection in resultRemoteChainStates)
            {
                tasks.Add(connection.Item1.GetTransactions(connection.Item2));
            }
            Task.WaitAll(tasks.ToArray());
            foreach (var taskResult in tasks.Select(t => t.Result))
            {
                if (taskResult != null)
                {
                    foreach (var transaction in taskResult)
                    {
                        _blockChain.AddTransaction(transaction);
                    }
                }
            }
        }

        private async void syncBlockchainsThread()
        {
            while (IsSyncing)
            {
                var lastBlock = _blockChain.LastBlock();
                if (lastBlock != null)
                {
                    var localBlockchainHash = _blockChain.BlockchainHash(lastBlock.Height);
                    var notSynnchronChains = new List<NodeConnection>();
                    foreach (var connection in _connections.ToList())
                    {
                        var blockchainHash = await connection.BlockchainHash(lastBlock.Height);
                        if (blockchainHash != localBlockchainHash)
                        {
                            notSynnchronChains.Add(connection);
                            break;
                        }
                    }
                    if (notSynnchronChains.Count > 0)
                    {
                        IsSynced = false;
                        _isBlockchainSynced = false;
                        syncBlockchains(notSynnchronChains, lastBlock);
                    }
                    else
                    {
                        _isBlockchainSynced = true;
                        for (int i = 0; i < 20; i++)
                        {
                            await Task.Delay(1000);
                            if (!IsSyncing)
                                break;
                        }
                    }
                }
                else
                    _isBlockchainSynced = true;
            }
        }

        private async void syncBlockchains(List<NodeConnection> notSyncNodeConnections, Block lastBlock)
        {
            var tasks = new List<Task<List<Transaction>>>();
            foreach (var connection in notSyncNodeConnections)
            {
                var lastBlockHeight = await connection.LastBlockHeight();
                if (lastBlockHeight > lastBlock.Height)
                {
                    var conBlockHashes = await connection.BlockHashes();
                    if (conBlockHashes != null)
                    {
                        var localBlockHashes = _blockChain.BlockHashes();
                        var smallerListEnd = conBlockHashes.Count < localBlockHashes.Count ? conBlockHashes.Count : localBlockHashes.Count;
                        var indexNotSame = 0;
                        for (int i = 0; i < smallerListEnd; i++)
                        {
                            if (conBlockHashes[i] != localBlockHashes[i])
                            {
                                indexNotSame = i + 1;
                                break;
                            }
                        }
                        if (indexNotSame > 0)
                        {
                            try
                            {
                                if (conBlockHashes.Count - indexNotSame < 50)
                                {
                                    var alternateBlockChain = await connection.GetBlocks(Enumerable.Range(indexNotSame, conBlockHashes.Count + 1).ToList());
                                    using (BlockchainDbContext db = new BlockchainDbContext())
                                    {

                                        var blocksToDelete = db.Blocks.Where(b => b.Height >= indexNotSame).ToList();
                                        blocksToDelete.ForEach(b => db.Blocks.Remove(b));
                                        db.SaveChanges();
                                        alternateBlockChain.ForEach(b => db.Blocks.Add(b));
                                        db.SaveChanges();
                                    }
                                }
                                else
                                {
                                    if (_blockChain.NextBlock != null)
                                    {
                                        _blockChain.NextBlock.StopMining();
                                        await Task.Delay(5000);
                                    }
                                    using (BlockchainDbContext db = new BlockchainDbContext())
                                    {
                                        var blocksToDelete = db.Blocks.ToList();
                                        blocksToDelete.ForEach(b => db.Blocks.Remove(b));
                                        db.SaveChanges();
                                    }
                                }
                            }
                            catch (Exception exc)
                            {

                            }
                        }
                    }
                }
            }
        }

        public async void BroadcastBlock(Block block)
        {
            if (_connections != null)
                foreach (var connection in _connections.ToList())
                {
                    await connection.PushBlock(block);
                }
        }

        public async void BroadcastTransaction(Transaction transaction)
        {
            if (_connections != null)
                foreach (var connection in _connections.ToList())
                {
                    await connection.PushTransaction(transaction);
                }
        }
    }
}
