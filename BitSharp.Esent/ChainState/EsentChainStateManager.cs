﻿using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Domain;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using BitSharp.Core.Storage.Memory;
using BitSharp.Esent.ChainState;
using BitSharp.Node.Storage;
using Microsoft.Isam.Esent.Collections.Generic;
using Microsoft.Isam.Esent.Interop;
using Microsoft.Isam.Esent.Interop.Windows81;
using Ninject;
using Ninject.Modules;
using Ninject.Parameters;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Esent
{
    internal class EsentChainStateManager : IDisposable
    {
        private readonly Logger logger;
        private readonly string baseDirectory;
        private readonly string jetDirectory;
        private readonly string jetDatabase;

        private Instance jetInstance;
        private IChainStateCursor chainStateCursor;

        private readonly object chainStateCursorLock;

        public EsentChainStateManager(string baseDirectory, Logger logger)
        {
            this.logger = logger;
            this.baseDirectory = baseDirectory;
            this.jetDirectory = Path.Combine(baseDirectory, "ChainState");
            this.jetDatabase = Path.Combine(this.jetDirectory, "ChainState.edb");

            this.chainStateCursorLock = new object();
        }

        public void Dispose()
        {
            new IDisposable[] {
                this.jetInstance
            }.DisposeList();
        }

        public IChainStateCursor CreateOrLoadChainState()
        {
            lock (this.chainStateCursorLock)
            {
                if (this.chainStateCursor != null)
                    throw new InvalidOperationException();

                this.jetInstance = CreateInstance(this.jetDirectory);
                this.jetInstance.Init();

                this.CreateOrOpenDatabase(this.jetDirectory, this.jetDatabase, this.jetInstance);

                this.chainStateCursor = new ChainStateCursor(this.jetDatabase, this.jetInstance, this.logger);

                return this.chainStateCursor;
            }
        }

        private void CreateOrOpenDatabase(string jetDirectory, string jetDatabase, Instance jetInstance)
        {
            try
            {
                ChainStateSchema.OpenDatabase(jetDatabase, jetInstance, readOnly: false);
            }
            catch (Exception)
            {
                try { Directory.Delete(jetDirectory, recursive: true); }
                catch (Exception) { }
                Directory.CreateDirectory(jetDirectory);

                ChainStateSchema.CreateDatabase(jetDatabase, jetInstance);
            }
        }

        private static Instance CreateInstance(string directory)
        {
            var instance = new Instance(Guid.NewGuid().ToString());

            instance.Parameters.SystemDirectory = directory;
            instance.Parameters.LogFileDirectory = directory;
            instance.Parameters.TempDirectory = directory;
            instance.Parameters.AlternateDatabaseRecoveryDirectory = directory;
            instance.Parameters.CreatePathIfNotExist = true;
            instance.Parameters.BaseName = "epc";
            instance.Parameters.EnableIndexChecking = false;
            instance.Parameters.CircularLog = true;
            instance.Parameters.CheckpointDepthMax = 64 * 1024 * 1024;
            instance.Parameters.LogFileSize = 1024 * 32;
            instance.Parameters.LogBuffers = 1024 * 32;
            instance.Parameters.MaxTemporaryTables = 1;
            instance.Parameters.MaxVerPages = 1024 * 256;
            instance.Parameters.NoInformationEvent = true;
            instance.Parameters.WaypointLatency = 1;
            instance.Parameters.MaxSessions = 256;
            instance.Parameters.MaxOpenTables = 256;
            if (EsentVersion.SupportsWindows81Features)
            {
                instance.Parameters.EnableShrinkDatabase = ShrinkDatabaseGrbit.On | ShrinkDatabaseGrbit.Realtime;
            }

            return instance;
        }
    }
}
