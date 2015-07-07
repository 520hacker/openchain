﻿using Microsoft.Framework.ConfigurationModel;
using OpenChain.Core;
using OpenChain.Core.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenChain.Initialization
{
    public static class StorageConfigurator
    {
        public static ILedgerStore CreateLedgerStore(IServiceProvider serviceProvider)
        {
            IConfiguration configuration = (IConfiguration)serviceProvider.GetService(typeof(IConfiguration));

            return new SqliteTransactionStore(configuration.GetSubKey("SQLite").Get("path"));
        }
    }
}
