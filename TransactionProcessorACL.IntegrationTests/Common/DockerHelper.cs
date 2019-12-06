﻿using System;
using System.Collections.Generic;
using System.Text;

namespace TransactionProcessor.IntegrationTests.Common
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Client;
    using Ductus.FluentDocker.Builders;
    using Ductus.FluentDocker.Model.Builders;
    using Ductus.FluentDocker.Services;
    using Ductus.FluentDocker.Services.Extensions;
    using EstateManagement.Client;

    public class DockerHelper
    {
        protected INetworkService TestNetwork;

        protected Int32 EstateManagementPort;
        protected Int32 TransactionProcessorPort;
        protected Int32 TransactionProcessorACLPort;
        protected Int32 EventStorePort;

        protected IContainerService EstateManagementContainer;
        protected IContainerService TransactionProcessorContainer;
        protected IContainerService TransactionProcessorACLContainer;
        protected IContainerService EventStoreContainer;

        public IEstateClient EstateClient;
        public HttpClient HttpClient;

        protected String EventStoreConnectionString;

        protected String EstateManagementContainerName;
        protected String TransactionProcessorContainerName;
        protected String TransactionProcessorACLContainerName;
        protected String EventStoreContainerName;

        private void SetupTestNetwork()
        {
            // Build a network
            this.TestNetwork = new Ductus.FluentDocker.Builders.Builder().UseNetwork($"testnetwork{Guid.NewGuid()}").Build();
        }
        public Guid TestId;
        private void SetupEventStoreContainer(String traceFolder)
        {
            // Event Store Container
            this.EventStoreContainer = new Ductus.FluentDocker.Builders.Builder()
                                       .UseContainer()
                                       .UseImage("eventstore/eventstore:release-5.0.2")
                                       .ExposePort(2113)
                                       .ExposePort(1113)
                                       .WithName(this.EventStoreContainerName)
                                       .WithEnvironment("EVENTSTORE_RUN_PROJECTIONS=all", "EVENTSTORE_START_STANDARD_PROJECTIONS=true")
                                       .UseNetwork(this.TestNetwork)
                                       .Mount(traceFolder, "/var/log/eventstore", MountType.ReadWrite)
                                       .Build()
                                       .Start().WaitForPort("2113/tcp", 30000);
        }

        public async Task StartContainersForScenarioRun(String scenarioName)
        {
            String traceFolder = $"/home/ubuntu/estatemanagement/trace/{scenarioName}/";

            Logging.Enabled();

            Guid testGuid = Guid.NewGuid();
            this.TestId = testGuid;

            // Setup the container names
            this.EstateManagementContainerName = $"estate{testGuid:N}";
            this.TransactionProcessorContainerName = $"txnprocessor{testGuid:N}";
            this.TransactionProcessorACLContainerName = $"txnprocessoracl{testGuid:N}";
            this.EventStoreContainerName = $"eventstore{testGuid:N}";

            this.EventStoreConnectionString =
                $"EventStoreSettings:ConnectionString=ConnectTo=tcp://admin:changeit@{this.EventStoreContainerName}:1113;VerboseLogging=true;";

            this.SetupTestNetwork();
            this.SetupEventStoreContainer(traceFolder);
            this.SetupEstateManagementContainer(traceFolder);
            this.SetupTransactionProcessorContainer(traceFolder);
            this.SetupTransactionProcessorACLContainer(traceFolder);

            // Cache the ports
            this.EstateManagementPort = this.EstateManagementContainer.ToHostExposedEndpoint("5000/tcp").Port;
            this.TransactionProcessorPort = this.TransactionProcessorContainer.ToHostExposedEndpoint("5002/tcp").Port;
            this.TransactionProcessorACLPort = this.TransactionProcessorACLContainer.ToHostExposedEndpoint("5003/tcp").Port;
            this.EventStorePort = this.EventStoreContainer.ToHostExposedEndpoint("2113/tcp").Port;

            // Setup the base address resolver
            Func<String, String> estateManagementBaseAddressResolver = api => $"http://127.0.0.1:{this.EstateManagementPort}";
            Func<String, String> transactionProcessorACLBaseAddressResolver = api => $"http://127.0.0.1:{this.TransactionProcessorACLPort}";

            HttpClient httpClient = new HttpClient();
            this.EstateClient = new EstateClient(estateManagementBaseAddressResolver, httpClient);

            // TODO: Use this to talk to txn processor scl until we have a client
            this.HttpClient = new HttpClient();
            this.HttpClient.BaseAddress = new Uri(transactionProcessorACLBaseAddressResolver(String.Empty));
        }

        public async Task StopContainersForScenarioRun()
        {
            try
            {
                if (this.TransactionProcessorACLContainer != null)
                {
                    this.TransactionProcessorACLContainer.StopOnDispose = true;
                    this.TransactionProcessorACLContainer.RemoveOnDispose = true;
                    this.TransactionProcessorACLContainer.Dispose();
                }

                if (this.TransactionProcessorContainer != null)
                {
                    this.TransactionProcessorContainer.StopOnDispose = true;
                    this.TransactionProcessorContainer.RemoveOnDispose = true;
                    this.TransactionProcessorContainer.Dispose();
                }

                if (this.EstateManagementContainer != null)
                {
                    this.EstateManagementContainer.StopOnDispose = true;
                    this.EstateManagementContainer.RemoveOnDispose = true;
                    this.EstateManagementContainer.Dispose();
                }

                if (this.EventStoreContainer != null)
                {
                    this.EventStoreContainer.StopOnDispose = true;
                    this.EventStoreContainer.RemoveOnDispose = true;
                    this.EventStoreContainer.Dispose();
                }

                if (this.TestNetwork != null)
                {
                    this.TestNetwork.Stop();
                    this.TestNetwork.Remove(true);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void SetupEstateManagementContainer(String traceFolder)
        {
            // Management API Container
            this.EstateManagementContainer = new Builder()
                                                .UseContainer()
                                                .WithName(this.EstateManagementContainerName)
                                                .WithEnvironment(this.EventStoreConnectionString) //,
                                                                                                  //"AppSettings:MigrateDatabase=true",
                                                                                                  //"EventStoreSettings:START_PROJECTIONS=true",
                                                                                                  //"EventStoreSettings:ContinuousProjectionsFolder=/app/projections/continuous")
                                                .WithCredential("https://www.docker.com", "stuartferguson", "Sc0tland")
                                                .UseImage("stuartferguson/estatemanagement")
                                                .ExposePort(5000)
                                                .UseNetwork(new List<INetworkService> { this.TestNetwork, Setup.DatabaseServerNetwork }.ToArray())
                                                .Mount(traceFolder, "/home", MountType.ReadWrite)
                                                .Build()
                                                .Start().WaitForPort("5000/tcp", 30000);
        }

        private void SetupTransactionProcessorContainer(String traceFolder)
        {
            // Management API Container
            this.TransactionProcessorContainer = new Builder()
                                                .UseContainer()
                                                .WithName(this.TransactionProcessorContainerName)
                                                .WithEnvironment(this.EventStoreConnectionString) //,
                                                                                                  //"AppSettings:MigrateDatabase=true",
                                                                                                  //"EventStoreSettings:START_PROJECTIONS=true",
                                                                                                  //"EventStoreSettings:ContinuousProjectionsFolder=/app/projections/continuous")
                                                .UseImage("stuartferguson/transactionprocessor")
                                                .ExposePort(5002)
                                                .UseNetwork(new List<INetworkService> { this.TestNetwork, Setup.DatabaseServerNetwork }.ToArray())
                                                .Mount(traceFolder, "/home", MountType.ReadWrite)
                                                .Build()
                                                .Start().WaitForPort("5002/tcp", 30000);
        }

        private void SetupTransactionProcessorACLContainer(String traceFolder)
        {
            // Management API Container
            this.TransactionProcessorACLContainer = new Builder()
                                                 .UseContainer()
                                                 .WithName(this.TransactionProcessorACLContainerName)
                                                 .UseImage("transactionprocessoracl")
                                                 .ExposePort(5003)
                                                 .UseNetwork(new List<INetworkService> { this.TestNetwork }.ToArray())
                                                 .Mount(traceFolder, "/home", MountType.ReadWrite)
                                                 .Build()
                                                 .Start().WaitForPort("5003/tcp", 30000);
        }
    }
}
