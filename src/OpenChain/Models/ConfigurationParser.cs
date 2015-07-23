﻿using System;
using System.Linq;
using System.Threading;
using Microsoft.Framework.Configuration;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using OpenChain.Core;
using OpenChain.Ledger;
using OpenChain.Sqlite;

namespace OpenChain.Models
{
    public static class ConfigurationParser
    {
        public static ITransactionStore CreateLedgerStore(IServiceProvider serviceProvider)
        {
            IConfiguration configuration = serviceProvider.GetService<IConfiguration>();
            IConfiguration storage = configuration.GetConfigurationSection("storage");
            if (storage["type"] == "SQLite")
                return new SqliteLedgerQueries(storage["path"]);
            else
                throw new NotSupportedException();
        }

        public static ILedgerQueries CreateLedgerQueries(IServiceProvider serviceProvider)
        {
            IConfiguration configuration = serviceProvider.GetService<IConfiguration>();
            ITransactionStore store = serviceProvider.GetService<ITransactionStore>();

            return store as ILedgerQueries;
        }

        public static IRulesValidator CreateRulesValidator(IServiceProvider serviceProvider)
        {
            IConfiguration configuration = serviceProvider.GetService<IConfiguration>().GetConfigurationSection("master_mode");
            ILogger logger = serviceProvider.GetService<ILogger>();

            if (configuration["root_url"] != null)
            {
                logger.LogInformation("Transaction validation mode enabled (Master mode)");
                IConfiguration validator = configuration.GetConfigurationSection("validator");

                switch (validator["type"])
                {
                    case "OpenLoop":
                        string[] adminAddresses = validator.GetConfigurationSections("admin_addresses").Select(key => validator.GetConfigurationSection("admin_addresses").Get(key.Key)).ToArray();
                        bool allowThirdPartyAssets = bool.Parse(validator["allow_third_party_assets"]);
                        return new OpenLoopValidator(serviceProvider.GetRequiredService<ITransactionStore>(), adminAddresses, allowThirdPartyAssets);
                    case "Disabled":
                        return ActivatorUtilities.CreateInstance<NullValidator>(serviceProvider, true);
                    default:
                        return null;
                }
            }
            else
            {
                logger.LogInformation("Transaction validation mode disabled (Slave mode)");
                return null;
            }
        }

        public static TransactionValidator CreateTransactionValidator(IServiceProvider serviceProvider)
        {
            IRulesValidator rulesValidator = serviceProvider.GetService<IRulesValidator>();

            if (rulesValidator == null)
                return null;
            else
                return new TransactionValidator(serviceProvider.GetService<ITransactionStore>(), rulesValidator, serviceProvider.GetService<IConfiguration>().Get("master_mode:root_url"));
        }

        public static MasterProperties CreateMasterProperties(IServiceProvider serviceProvider)
        {
            IRulesValidator master = serviceProvider.GetService<IRulesValidator>();

            if (master != null)
            {
                IConfiguration configuration = serviceProvider.GetService<IConfiguration>().GetConfigurationSection("master_mode").GetConfigurationSection("properties");
                return new MasterProperties(configuration);
            }
            else
            {
                return null;
            }
        }

        public static ILogger CreateLogger(IServiceProvider serviceProvider)
        {
            IConfiguration configuration = serviceProvider.GetService<IConfiguration>();
            ILoggerFactory loggerFactory = serviceProvider.GetService<ILoggerFactory>();

            return loggerFactory.CreateLogger("General");
        }

        public static IStreamSubscriber CreateStreamSubscriber(IServiceProvider serviceProvider)
        {
            ILogger logger = serviceProvider.GetService<ILogger>();

            if (serviceProvider.GetService<IRulesValidator>() != null)
            {
                logger.LogInformation("Stream subscriber disabled");
                return null;
            }
            else
            {
                IConfiguration observerMode = serviceProvider.GetService<IConfiguration>().GetConfigurationSection("observer_mode");
                
                string masterUrl = observerMode["master_url"];
                logger.LogInformation("Stream subscriber enabled, master URL: {0}", masterUrl);
                TransactionStreamSubscriber streamSubscriber = ActivatorUtilities.CreateInstance<TransactionStreamSubscriber>(serviceProvider, new Uri(masterUrl));
                streamSubscriber.Subscribe(CancellationToken.None);

                return streamSubscriber;
            }
        }
    }
}
