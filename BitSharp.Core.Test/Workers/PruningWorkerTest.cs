﻿using BitSharp.Common;
using BitSharp.Core.Builders;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage.Memory;
using BitSharp.Core.Test.Rules;
using BitSharp.Core.Workers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace BitSharp.Core.Test.Workers
{
    [TestClass]
    public class PruningWorkerTest
    {
        [TestMethod]
        public void TestPruneAllData()
        {
            var logger = LogManager.CreateNullLogger();

            // create a block
            var txCount = 100;
            var block = CreateFakeBlock(txCount);
            var chainedHeader = new ChainedHeader(block.Header, height: 0, totalWork: block.Header.CalculateWork());

            // create a long chain based off the block, to account for pruning buffer
            var fakeHeaders = new FakeHeaders(new[] { chainedHeader.BlockHeader });
            var chain = new ChainBuilder(Enumerable.Concat(new[] { chainedHeader }, Enumerable.Range(0, 2000).Select(x => fakeHeaders.NextChained()))).ToImmutable();

            // mock core daemon to return the chain
            var coreDaemon = new Mock<ICoreDaemon>();
            coreDaemon.Setup(x => x.CurrentChain).Returns(chain);

            // create memory storage with the block
            var storageManager = new MemoryStorageManager();
            storageManager.BlockTxesStorage.TryAddBlockTransactions(block.Hash, block.Transactions);

            // initialize the pruning worker
            var workerConfig = new WorkerConfig(initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue);
            using (var pruningWorker = new PruningWorker(workerConfig, coreDaemon.Object, storageManager, null))
            // get a chain state cursor
            using (var handle = storageManager.OpenChainStateCursor())
            {
                var chainStateCursor = handle.Item;

                // set the pruning worker to prune all data
                pruningWorker.Mode = PruningMode.ReplayAndRollbackAndTxes;

                // wire event to wait for work
                var workFinishedEvent = new AutoResetEvent(false);
                pruningWorker.OnWorkFinished += () => workFinishedEvent.Set();

                // wire event to track exceptions
                Exception workException = null;
                pruningWorker.OnWorkError += e => workException = e;

                // start the worker
                pruningWorker.Start();

                // pick a random pruning order
                var random = new Random();
                var pruneOrderSource = Enumerable.Range(0, txCount).ToList();
                var pruneOrder = new List<int>(txCount);
                while (pruneOrderSource.Count > 0)
                {
                    var randomIndex = random.Next(pruneOrderSource.Count);

                    pruneOrder.Add(pruneOrderSource[randomIndex]);
                    pruneOrderSource.RemoveAt(randomIndex);
                }

                // add an unspent tx for each transaction to storage
                for (var txIndex = 0; txIndex < block.Transactions.Length;txIndex++)
                {
                    var tx = block.Transactions[txIndex];
                    var unspentTx = new UnspentTx(tx.Hash, blockIndex: 0, txIndex: txIndex, outputStates: new OutputStates(1, OutputState.Unspent));
                    chainStateCursor.TryAddUnspentTx(unspentTx);
                }

                // create a memory pruning cursor to verify expected pruning results
                var pruneCursor = new MemoryMerkleTreePruningCursor(block.Transactions);

                // prune each transaction in random order
                var pruneHeight = -1;
                foreach (var pruneTxIndex in pruneOrder)
                {
                    // create a spent tx to prune the transaction
                    var pruneTx = block.Transactions[pruneTxIndex];
                    var spentTx = new SpentTx(txHash: pruneTx.Hash, confirmedBlockIndex: 0, txIndex: pruneTxIndex, outputCount: 1, spentBlockIndex: 0);
                    var spentTxes = ImmutableList.Create(spentTx);

                    // store the spent txes for the current pruning block
                    pruneHeight++;
                    Assert.IsTrue(chainStateCursor.TryAddBlockSpentTxes(pruneHeight, spentTxes));
                    pruningWorker.PrunableHeight = pruneHeight;

                    // verify unspent tx is present before pruning
                    Assert.IsTrue(chainStateCursor.ContainsUnspentTx(pruneTx.Hash));

                    // notify the pruning worker and wait
                    pruningWorker.NotifyWork();
                    workFinishedEvent.WaitOne();

                    // verify unspent tx is removed after pruning
                    Assert.IsFalse(chainStateCursor.ContainsUnspentTx(pruneTx.Hash));

                    // verify the spent txes were removed
                    Assert.IsFalse(chainStateCursor.ContainsBlockSpentTxes(pruneHeight));

                    // prune to determine expected results
                    MerkleTree.PruneNode(pruneCursor, pruneTxIndex);
                    var expectedPrunedTxes = pruneCursor.ReadNodes().ToList();

                    // retrieve the actual transaction after pruning
                    IEnumerable<BlockTx> actualPrunedTxes;
                    Assert.IsTrue(storageManager.BlockTxesStorage.TryReadBlockTransactions(block.Hash, out actualPrunedTxes));

                    // verify the actual pruned transactions match the expected results
                    CollectionAssert.AreEqual(expectedPrunedTxes, actualPrunedTxes.ToList());
                }

                // verify all unspent txes were removed
                Assert.AreEqual(0, chainStateCursor.ReadUnspentTransactions().Count());

                // verify final block with all transactions pruned
                IEnumerable<BlockTx> finalPrunedTxes;
                Assert.IsTrue(storageManager.BlockTxesStorage.TryReadBlockTransactions(block.Hash, out finalPrunedTxes));
                Assert.AreEqual(1, finalPrunedTxes.Count());
                Assert.AreEqual(block.Header.MerkleRoot, finalPrunedTxes.Single().Hash);

                // verify no work exceptions occurred
                Assert.IsNull(workException);
            }
        }

        //TODO copy pasted
        private Block CreateFakeBlock(int txCount)
        {
            var transactions = Enumerable.Range(0, txCount).Select(x => RandomData.RandomTransaction()).ToImmutableArray();
            var blockHeader = RandomData.RandomBlockHeader().With(MerkleRoot: MerkleTree.CalculateMerkleRoot(transactions), Bits: DataCalculator.TargetToBits(UnitTestRules.Target0));
            var block = new Block(blockHeader, transactions);

            return block;
        }
    }
}
