﻿using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Builders;
using BitSharp.Core.Domain;
using BitSharp.Core.Script;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace BitSharp.Core.Test
{
    [TestClass]
    public class BlockReplayerTest
    {
        //TODO this is copied in source multiple times
        private const UInt64 SATOSHI_PER_BTC = 100 * 1000 * 1000;

        [TestMethod]
        [Timeout(300000/*ms*/)]
        public void TestReplayBlock()
        {
            var logger = LogManager.CreateNullLogger();

            using (var simulator = new MainnetSimulator())
            using (var blockReplayer = new BlockReplayer(simulator.CoreDaemon.CoreStorage, simulator.CoreDaemon.Rules))
            {
                simulator.AddBlockRange(0, 9999);
                simulator.WaitForUpdate();

                using (var chainState = simulator.CoreDaemon.GetChainState())
                {
                    Assert.AreEqual(9999, chainState.Chain.Height);

                    for (var blockHeight = 0; blockHeight <= chainState.Chain.Height; blockHeight++)
                    {
                        var blockHash = chainState.Chain.Blocks[blockHeight].Hash;

                        using (blockReplayer.StartReplay(chainState, blockHash))
                        {
                            var expectedTransactions = simulator.BlockProvider.GetBlock(blockHeight).Transactions;
                            var actualTransactions = blockReplayer.ReplayBlock().OrderBy(x => x.TxIndex).ToList();

                            CollectionAssert.AreEqual(expectedTransactions, actualTransactions, new TxHashComparer(), "Transactions differ at block {0:#,##0}".Format2(blockHeight));
                        }
                    }
                }
            }
        }

        // this test replays a rollback 2 blocks deep (creates blocks 0-3, 2-3 are rolled back)
        // block 3 spends a transaction in block 2
        // because block 2 is rolled back, the UnspentTx information that block 3 needs will be removed entirely from the chain state
        //
        // this test verifies the information needed to replay a rolled-back block
        [TestMethod]
        public void TestReplayBlockRollback()
        {
            var logger = LogManager.CreateNullLogger();

            using (var daemon = new TestDaemon())
            using (var blockReplayer = new BlockReplayer(daemon.CoreDaemon.CoreStorage, daemon.CoreDaemon.Rules))
            {
                // create a new keypair to spend to
                var toKeyPair = daemon.TxManager.CreateKeyPair();
                var toPrivateKey = toKeyPair.Item1;
                var toPublicKey = toKeyPair.Item2;

                // add block 1
                var block0 = daemon.GenesisBlock;
                var block1 = daemon.MineAndAddEmptyBlock(block0);

                // add block 2, spending from block 1
                var spendTx1 = daemon.TxManager.CreateSpendTransaction(block1.Transactions[0], 0, (byte)ScriptHashType.SIGHASH_ALL, block1.Transactions[0].OutputValue(), daemon.CoinbasePrivateKey, daemon.CoinbasePublicKey, toPublicKey);
                var block2Unmined = daemon.CreateEmptyBlock(block1)
                    .WithAddedTransactions(spendTx1);
                var block2 = daemon.MineAndAddBlock(block2Unmined);

                // add block 3, spending from block 2
                var spendTx2 = daemon.TxManager.CreateSpendTransaction(block2.Transactions[1], 0, (byte)ScriptHashType.SIGHASH_ALL, block2.Transactions[1].OutputValue(), toPrivateKey, toPublicKey, toPublicKey);
                var block3Unmined = daemon.CreateEmptyBlock(block2)
                    .WithAddedTransactions(spendTx2);
                var block3 = daemon.MineAndAddBlock(block3Unmined);

                // replay all blocks up to block 3
                daemon.WaitForUpdate();
                using (var chainState = daemon.CoreDaemon.GetChainState())
                {
                    Assert.AreEqual(3, chainState.Chain.Height);

                    var replayTransactions = new List<TxWithPrevOutputs>();
                    foreach (var blockHash in chainState.Chain.Blocks.Select(x => x.Hash))
                    {
                        using (blockReplayer.StartReplay(chainState, blockHash))
                        {
                            replayTransactions.AddRange(
                                blockReplayer.ReplayBlock().OrderBy(x => x.TxIndex));
                        }
                    }
                    
                    // verify all transactions were replayed
                    Assert.AreEqual(6, replayTransactions.Count);
                    Assert.AreEqual(block0.Transactions[0].Hash, replayTransactions[0].Transaction.Hash);
                    Assert.AreEqual(block1.Transactions[0].Hash, replayTransactions[1].Transaction.Hash);
                    Assert.AreEqual(block2.Transactions[0].Hash, replayTransactions[2].Transaction.Hash);
                    Assert.AreEqual(block2.Transactions[1].Hash, replayTransactions[3].Transaction.Hash);
                    Assert.AreEqual(block3.Transactions[0].Hash, replayTransactions[4].Transaction.Hash);
                    Assert.AreEqual(block3.Transactions[1].Hash, replayTransactions[5].Transaction.Hash);
                }

                // mark blocks 2-3 invalid, they will be rolled back
                daemon.CoreStorage.MarkBlockInvalid(block3.Hash);
                daemon.CoreStorage.MarkBlockInvalid(block2.Hash);
                daemon.WaitForUpdate();

                // replay rollback of block 3
                using (var chainState = daemon.CoreDaemon.GetChainState())
                {
                    Assert.AreEqual(1, chainState.Chain.Height);

                    var replayTransactions = new List<TxWithPrevOutputs>();
                    using (blockReplayer.StartReplay(chainState, block3.Hash))
                    {
                        replayTransactions.AddRange(
                            blockReplayer.ReplayBlock().OrderBy(x => -x.TxIndex));
                    }

                    // verify transactions were replayed
                    Assert.AreEqual(2, replayTransactions.Count);
                    Assert.AreEqual(block3.Transactions[1].Hash, replayTransactions[0].Transaction.Hash);
                    Assert.AreEqual(block3.Transactions[0].Hash, replayTransactions[1].Transaction.Hash);

                    // verify correct previous output was replayed (block 3 tx 1 spent block 2 tx 1)
                    Assert.AreEqual(1, replayTransactions[0].PrevTxOutputs.Length);
                    CollectionAssert.AreEqual(block2.Transactions[1].Outputs[0].ScriptPublicKey, replayTransactions[0].PrevTxOutputs[0].ScriptPublicKey);

                    // verify correct previous output was replayed (block 3 tx 0 spends nothing, coinbase)
                    Assert.AreEqual(0, replayTransactions[1].PrevTxOutputs.Length);
                }

                // replay rollback of block 2
                using (var chainState = daemon.CoreDaemon.GetChainState())
                {
                    Assert.AreEqual(1, chainState.Chain.Height);

                    var replayTransactions = new List<TxWithPrevOutputs>();
                    using (blockReplayer.StartReplay(chainState, block2.Hash))
                    {
                        replayTransactions.AddRange(
                            blockReplayer.ReplayBlock().OrderBy(x => -x.TxIndex));
                    }

                    // verify transactions were replayed
                    Assert.AreEqual(2, replayTransactions.Count);
                    Assert.AreEqual(block2.Transactions[1].Hash, replayTransactions[0].Transaction.Hash);
                    Assert.AreEqual(block2.Transactions[0].Hash, replayTransactions[1].Transaction.Hash);

                    // verify correct previous output was replayed (block 2 tx 1 spent block 1 tx 0)
                    Assert.AreEqual(1, replayTransactions[0].PrevTxOutputs.Length);
                    CollectionAssert.AreEqual(block1.Transactions[0].Outputs[0].ScriptPublicKey, replayTransactions[0].PrevTxOutputs[0].ScriptPublicKey);

                    // verify correct previous output was replayed (block 2 tx 0 spends nothing, coinbase)
                    Assert.AreEqual(0, replayTransactions[1].PrevTxOutputs.Length);
                }
            }
        }

        private sealed class TxHashComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                var txX = (Transaction)x;
                var txY = (TxWithPrevOutputs)y;
                return txX.Hash.CompareTo(txY.Transaction.Hash);
            }
        }
    }
}
