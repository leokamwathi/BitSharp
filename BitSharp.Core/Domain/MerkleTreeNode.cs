﻿using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain
{
    public class MerkleTreeNode
    {
        private readonly int index;
        private readonly int depth;
        private readonly UInt256 hash;

        public MerkleTreeNode(int index, int depth, UInt256 hash)
        {
            this.index = index;
            this.depth = depth;
            this.hash = hash;
        }

        public int Index { get { return this.index; } }

        public int Depth { get { return this.depth; } }

        public UInt256 Hash { get { return this.hash; } }

        public bool IsLeft { get { return (this.index >> this.depth) % 2 == 0; } }

        public bool IsRight { get { return !this.IsLeft; } }

        public MerkleTreeNode PairWith(MerkleTreeNode right)
        {
            return Pair(this, right);
        }

        public MerkleTreeNode PairWithSelf()
        {
            return PairWithSelf(this);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is MerkleTreeNode))
                return false;

            var other = (MerkleTreeNode)obj;
            return other.index == this.index && other.depth == this.depth && other.hash == this.hash;
        }

        public override int GetHashCode()
        {
            return this.index.GetHashCode() ^ this.depth.GetHashCode() ^ this.hash.GetHashCode();
        }

        public static MerkleTreeNode Pair(MerkleTreeNode left, MerkleTreeNode right)
        {
            if (left.Depth != right.Depth)
                throw new InvalidOperationException();

            var expectedIndex = left.Index + (1 << left.Depth);
            if (right.Index != expectedIndex)
                throw new InvalidOperationException();

            var pairHashBytes = new byte[64];
            left.Hash.ToByteArray(pairHashBytes, 0);
            right.Hash.ToByteArray(pairHashBytes, 32);

            var sha256 = new SHA256Managed();
            var pairHash = new UInt256(sha256.ComputeDoubleHash(pairHashBytes));

            return new MerkleTreeNode(left.Index, left.Depth + 1, pairHash);
        }

        public static MerkleTreeNode PairWithSelf(MerkleTreeNode left)
        {
            return Pair(left, new MerkleTreeNode(left.index + (1 << left.depth), left.depth, left.hash));
        }

        public static bool operator ==(MerkleTreeNode left, MerkleTreeNode right)
        {
            return object.ReferenceEquals(left, right) || (!object.ReferenceEquals(left, null) && !object.ReferenceEquals(right, null) && left.Equals(right));
        }

        public static bool operator !=(MerkleTreeNode left, MerkleTreeNode right)
        {
            return !(left == right);
        }
    }
}
