﻿using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain.Test
{
    [TestClass]
    public class UtxoTest
    {
        [TestMethod]
        public void TestCanSpend_Unspent()
        {
            // prepare utxo storage
            var unspentTransactions = ImmutableDictionary.CreateBuilder<UInt256, UnspentTx>();

            // prepare unspent output
            var txHash = new UInt256(0);
            unspentTransactions.Add(txHash, new UnspentTx(confirmedBlockHash: 0, length: 1, state: OutputState.Unspent));

            // prepare utxo
            var utxoStorage = new MemoryUtxoStorage(0, unspentTransactions.ToImmutable(), null);
            var utxo = new Utxo(utxoStorage);

            // prepare output reference
            var prevTxOutput = new TxOutputKey(txHash, txOutputIndex: 0);

            // check if output can be spent
            var canSpend = utxo.CanSpend(prevTxOutput);

            // verify output can be spent
            Assert.IsTrue(canSpend);
        }

        [TestMethod]
        public void TestCanSpend_Spent()
        {
            // prepare utxo storage
            var unspentTransactions = ImmutableDictionary.CreateBuilder<UInt256, UnspentTx>();

            // prepare spent output
            var txHash = new UInt256(0);
            unspentTransactions.Add(txHash, new UnspentTx(confirmedBlockHash: 0, length: 1, state: OutputState.Spent));

            // prepare utxo
            var utxoStorage = new MemoryUtxoStorage(0, unspentTransactions.ToImmutable(), null);
            var utxo = new Utxo(utxoStorage);

            // prepare output reference
            var prevTxOutput = new TxOutputKey(txHash, txOutputIndex: 0);

            // check if output can be spent
            var canSpend = utxo.CanSpend(prevTxOutput);

            // verify output cannot be spent
            Assert.IsFalse(canSpend);
        }

        [TestMethod]
        public void TestCanSpend_Missing()
        {
            // prepare utxo storage
            var unspentTransactions = ImmutableDictionary.CreateBuilder<UInt256, UnspentTx>();

            // prepare utxo
            var utxoStorage = new MemoryUtxoStorage(0, unspentTransactions.ToImmutable(), null);
            var utxo = new Utxo(utxoStorage);

            // prepare output reference
            var prevTxOutput = new TxOutputKey(txHash: 0, txOutputIndex: 0);

            // check if output can be spent
            var canSpend = utxo.CanSpend(prevTxOutput);

            // verify output cannot be spent
            Assert.IsFalse(canSpend);
        }

        [TestMethod]
        public void TestCanSpend_NegativeIndex()
        {
            // prepare utxo storage
            var unspentTransactions = ImmutableDictionary.CreateBuilder<UInt256, UnspentTx>();

            // prepare unspent output
            var txHash = new UInt256(0);
            unspentTransactions.Add(txHash, new UnspentTx(confirmedBlockHash: 0, length: 1, state: OutputState.Unspent));

            // prepare utxo
            var utxoStorage = new MemoryUtxoStorage(0, unspentTransactions.ToImmutable(), null);
            var utxo = new Utxo(utxoStorage);

            // prepare output reference
            var prevTxOutput = new TxOutputKey(txHash, txOutputIndex: UInt32.MaxValue);

            // check if output can be spent
            var canSpend = utxo.CanSpend(prevTxOutput);

            // verify output cannot be spent
            Assert.IsFalse(canSpend);
        }

        [TestMethod]
        public void TestCanSpend_IndexOutOfRange()
        {
            // prepare utxo storage
            var unspentTransactions = ImmutableDictionary.CreateBuilder<UInt256, UnspentTx>();

            // prepare unspent output
            var txHash = new UInt256(0);
            unspentTransactions.Add(txHash, new UnspentTx(confirmedBlockHash: 0, length: 1, state: OutputState.Unspent));

            // prepare utxo
            var utxoStorage = new MemoryUtxoStorage(0, unspentTransactions.ToImmutable(), null);
            var utxo = new Utxo(utxoStorage);

            // prepare output reference
            var prevTxOutput = new TxOutputKey(txHash, txOutputIndex: 1);

            // check if output can be spent
            var canSpend = utxo.CanSpend(prevTxOutput);

            // verify output cannot be spent
            Assert.IsFalse(canSpend);
        }
    }
}