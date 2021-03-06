﻿using BitSharp.Common.ExtensionMethods;
using BitSharp.Esent.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BitSharp.Core.Test.Storage
{
    [TestClass]
    public class StorageProviderTest
    {
        private readonly List<ITestStorageProvider> testStorageProviders =
            new List<ITestStorageProvider>
            {
                new MemoryTestStorageProvider(),
                new EsentTestStorageProvider(),
            };

        // Run the specified test method against all providers
        protected void RunTest(Action<ITestStorageProvider> testMethod)
        {
            foreach (var provider in testStorageProviders)
            {
                Debug.WriteLine("Testing provider: {0}".Format2(provider.Name));

                provider.TestInitialize();
                try
                {
                    testMethod(provider);
                }
                finally
                {
                    provider.TestCleanup();
                }
            }
        }
    }
}
