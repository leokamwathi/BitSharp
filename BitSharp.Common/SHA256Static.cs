﻿using System;
using System.IO;
using System.Security.Cryptography;

namespace BitSharp.Common
{
    public static class SHA256Static
    {
        [ThreadStatic]
        private static SHA256Managed sha256;

        public static byte[] ComputeHash(byte[] buffer)
        {
            var sha256 = GetSHA256();
            return sha256.ComputeHash(buffer);
        }

        public static byte[] ComputeDoubleHash(byte[] buffer)
        {
            var sha256 = GetSHA256();
            return sha256.ComputeHash(sha256.ComputeHash(buffer));
        }

        public static byte[] ComputeDoubleHash(Stream inputStream)
        {
            var sha256 = GetSHA256();
            return sha256.ComputeHash(sha256.ComputeHash(inputStream));
        }

        private static SHA256Managed GetSHA256()
        {
            if (sha256 == null)
                sha256 = new SHA256Managed();

            return sha256;
        }
    }
}
