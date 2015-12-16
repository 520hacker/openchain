﻿// Copyright 2015 Coinprism, Inc.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.WebSockets.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using Openchain.Ledger;
using Openchain.Ledger.Validation;
using Openchain.Server.Models;

namespace Openchain.Server
{
    public class Startup
    {
        private readonly IConfiguration configuration;
        private static List<Task> runningTasks = new List<Task>();

        public Startup(IHostingEnvironment env, IApplicationEnvironment application)
        {
            // Setup Configuration
            configuration = new ConfigurationBuilder()
                .SetBasePath(application.ApplicationBasePath)
                .AddJsonFile(env.MapPath("App_Data/config.json"))
                .AddUserSecrets()
                .AddEnvironmentVariables()
                .Build();
        }

        /// <summary>
        /// Adds services to the dependency injection container.
        /// </summary>
        /// <param name="services">The collection of services.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.BuildServiceProvider().GetService<ILoggerFactory>().AddConsole();

            services.AddSingleton<IConfiguration>(_ => this.configuration);

            // Setup ASP.NET MVC
            services
                .AddMvcCore()
                .AddViews()
                .AddJsonFormatters();

            // Logger
            services.AddTransient<ILogger>(ConfigurationParser.CreateLogger);

            LogStartup(services.BuildServiceProvider().GetService<ILogger>(), services.BuildServiceProvider().GetService<IApplicationEnvironment>());

            // CORS Headers
            services.AddCors();

            // Ledger Store
            services.AddTransient<IStorageEngine>(ConfigurationParser.CreateLedgerStore(services.BuildServiceProvider()));

            services.AddTransient<ILedgerQueries>(ConfigurationParser.CreateLedgerQueries);

            services.AddTransient<ILedgerIndexes>(ConfigurationParser.CreateLedgerIndexes);

            services.AddTransient<IAnchorBuilder>(ConfigurationParser.CreateAnchorBuilder);

            services.AddSingleton<IMutationValidator>(ConfigurationParser.CreateRulesValidator);

            services.AddTransient<TransactionValidator>(ConfigurationParser.CreateTransactionValidator);

            // Transaction Stream Subscriber
            services.AddSingleton<TransactionStreamSubscriber>(ConfigurationParser.CreateStreamSubscriber);

            // Anchoring
            services.AddSingleton<LedgerAnchorWorker>(ConfigurationParser.CreateLedgerAnchorWorker);
        }

        private static void LogStartup(ILogger logger, IApplicationEnvironment environment)
        {
            logger.LogInformation($"Starting Openchain v{environment.ApplicationVersion} ({environment.RuntimeFramework.FullName})");
            logger.LogInformation(" ");
        }

        /// <summary>
        /// Configures the services.
        /// </summary>
        public async void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerfactory, IConfiguration configuration, IStorageEngine store)
        {
            app.UseCors(builder => builder.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());

            app.UseIISPlatformHandler();

            app.Map("/stream", managedWebSocketsApp =>
            {
                if (bool.Parse(configuration["enable_transaction_stream"]))
                {
                    managedWebSocketsApp.UseWebSockets(new WebSocketOptions() { ReplaceFeature = true });
                    managedWebSocketsApp.Use(next => new TransactionStreamMiddleware(next).Invoke);
                }
            });

            // Add MVC to the request pipeline.
            app.UseMvc();

            await ConfigurationParser.InitializeLedgerStore(app.ApplicationServices);

            // Activate singletons
            TransactionStreamSubscriber subscriber = app.ApplicationServices.GetService<TransactionStreamSubscriber>();
            if (subscriber != null)
                runningTasks.Add(subscriber.Subscribe(CancellationToken.None));

            app.ApplicationServices.GetService<IMutationValidator>();

            LedgerAnchorWorker anchorWorker = app.ApplicationServices.GetService<LedgerAnchorWorker>();
            if (anchorWorker != null)
                runningTasks.Add(anchorWorker.Run(CancellationToken.None));
        }
    }
}
