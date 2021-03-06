﻿using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage.Memory;
using BitSharp.Node;
using BitSharp.Node.Network;
using BitSharp.Node.Storage;
using BitSharp.Node.Storage.Memory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ninject;
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace BitSharp.IntegrationTest
{
    [TestClass]
    public class PullTester
    {
        [TestMethod]
        [Timeout(300000/*ms*/)]
        public void TestPullTester()
        {
            // locate java.exe
            var javaPath = Path.Combine(Environment.GetEnvironmentVariable("JAVA_HOME"), "bin", "java.exe");
            if (!File.Exists(javaPath))
                Assert.Inconclusive("java.exe could not be found under JAVA_HOME");

            // prepare a temp folder for bitcoinj
            var tempFolder = Path.Combine(Path.GetTempPath(), "BitSharp", "Tests", Path.GetRandomFileName());
            var tempFile = Path.Combine(tempFolder, "BitcoindComparisonTool");
            Directory.CreateDirectory(tempFolder);
            try
            {
                // initialize kernel
                using (var kernel = new StandardKernel())
                {
                    // add logging module
                    kernel.Load(new ConsoleLoggingModule(LogLevel.Info));

                    // log startup
                    var logger = LogManager.GetCurrentClassLogger();
                    logger.Info("Starting up: {0}".Format2(DateTime.Now));

                    // add storage module
                    kernel.Load(new MemoryStorageModule());
                    kernel.Load(new NodeMemoryStorageModule());

                    // add cache modules
                    kernel.Load(new NodeCacheModule());

                    // add rules module
                    var rulesType = RulesEnum.ComparisonToolTestNet;
                    kernel.Load(new RulesModule(rulesType));

                    // initialize the blockchain daemon
                    using (var coreDaemon = kernel.Get<CoreDaemon>())
                    {
                        kernel.Bind<CoreDaemon>().ToConstant(coreDaemon).InTransientScope();

                        // initialize p2p client
                        using (var localClient = kernel.Get<LocalClient>())
                        {
                            kernel.Bind<LocalClient>().ToConstant(localClient).InTransientScope();

                            // start the blockchain daemon
                            coreDaemon.Start();

                            // find a free port
                            var port = FindFreePort();
                            Messaging.Port = port;

                            // start p2p client
                            localClient.Start();

                            // run pull tester
                            var runLargeReorgs = 0;
                            var javaProcessInfo = new ProcessStartInfo
                            {
                                FileName = javaPath,
                                Arguments = @"-jar pull-tests.jar {0} {1} {2}".Format2(tempFile, runLargeReorgs, port),
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            };
                            using (var javaProcess = Process.Start(javaProcessInfo))
                            {
                                javaProcess.OutputDataReceived += (sender, e) =>
                                    logger.Info("[Pull Tester]:  " + e.Data);
                                javaProcess.ErrorDataReceived += (sender, e) =>
                                    logger.Error("[Pull Tester]: " + e.Data);

                                javaProcess.BeginOutputReadLine();
                                javaProcess.BeginErrorReadLine();

                                javaProcess.WaitForExit();

                                logger.Info("Pull Tester Result: {0}".Format2(javaProcess.ExitCode));

                                Assert.Inconclusive("TODO");
                                Assert.AreEqual(0, javaProcess.ExitCode);
                            }
                        }
                    }
                }
            }
            finally
            {
                // cleanup the temp bitcoinj folder
                Directory.Delete(tempFolder, recursive: true);
            }
        }

        private int FindFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
