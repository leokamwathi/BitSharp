﻿using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.JsonRpc;
using BitSharp.Core.Monitor;
using BitSharp.Core.Rules;
using BitSharp.Core.Script;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Ninject;
using NLog;
using System.Collections.Concurrent;

namespace BitSharp.Core.Test.Monitor
{
    [TestClass]
    public class TransactionMonitorTest
    {
        [TestMethod]
        public void TestMonitorAddress()
        {
            var sha256 = new SHA256Managed();

            //var publicKey =
            //    "04ea1feff861b51fe3f5f8a3b12d0f4712db80e919548a80839fc47c6a21e66d957e9c5d8cd108c7a2d2324bad71f9904ac0ae7336507d785b17a2c115e427a32f"
            //    .HexToByteArray();

            var publicKey =
                "04f9804cfb86fb17441a6562b07c4ee8f012bdb2da5be022032e4b87100350ccc7c0f4d47078b06c9d22b0ec10bdce4c590e0d01aed618987a6caa8c94d74ee6dc"
                .HexToByteArray();

            var outputScript1 = new PayToPublicKeyBuilder().CreateOutput(publicKey);
            var outputScript1Hash = new UInt256(sha256.ComputeHash(outputScript1));
            var outputScript2 = new PayToPublicKeyHashBuilder().CreateOutputFromPublicKey(publicKey);
            var outputScript2Hash = new UInt256(sha256.ComputeHash(outputScript2));

            var outputScriptHashes = new ConcurrentSet<UInt256>();
            for (var i = 0; i < 1.MILLION(); i++)
                outputScriptHashes.Add(i);

            outputScriptHashes.Add(outputScript1Hash);
            outputScriptHashes.Add(outputScript2Hash);

            var txMonitor = new Mock<ITransactionMonitor>();

            var mintedTxOutputs = new ConcurrentBag<TxOutput>();
            var spentTxOutputs = new ConcurrentBag<TxOutput>();

            txMonitor.Setup(x => x.MintTxOutput(It.IsAny<TxOutput>())).Callback<TxOutput>(
                txOutput =>
                {
                    if (outputScriptHashes.Contains(new UInt256(sha256.ComputeHash(txOutput.ScriptPublicKey.ToArray()))))
                    {
                        Debug.WriteLine("+{0} BTC".Format2((decimal)txOutput.Value / 100.MILLION()));
                        mintedTxOutputs.Add(txOutput);
                    }
                });
            var outputScript = new PayToPublicKeyBuilder().CreateOutput(publicKey);
            var outputScriptHash = new UInt256(sha256.ComputeHash(outputScript));

            txMonitor.Setup(x => x.SpendTxOutput(It.IsAny<TxOutput>())).Callback<TxOutput>(
                txOutput =>
                {
                    if (outputScriptHashes.Contains(new UInt256(sha256.ComputeHash(txOutput.ScriptPublicKey.ToArray()))))
                    {
                        Debug.WriteLine("-{0} BTC".Format2((decimal)txOutput.Value / 100.MILLION()));
                        spentTxOutputs.Add(txOutput);
                    }
                });

            using (var simulator = new MainnetSimulator())
            {
                simulator.CoreDaemon.RegistorMonitor(txMonitor.Object);

                var block9999 = simulator.BlockProvider.GetBlock(9999);

                simulator.AddBlockRange(0, 9999);
                simulator.WaitForDaemon();
                simulator.CloseChainStateBuiler();
                AssertMethods.AssertDaemonAtBlock(9999, block9999.Hash, simulator.CoreDaemon);

                var actualMintedBtc = mintedTxOutputs.Sum(x => (decimal)x.Value) / 100.MILLION();
                var actualSpentBtc = spentTxOutputs.Sum(x => (decimal)x.Value) / 100.MILLION();

                Assert.AreEqual(16, mintedTxOutputs.Count);
                Assert.AreEqual(14, spentTxOutputs.Count);
                Assert.AreEqual(569.44M, actualMintedBtc);
                Assert.AreEqual(536.52M, actualSpentBtc);
            }
        }
    }
}
