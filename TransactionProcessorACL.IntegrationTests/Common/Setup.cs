﻿using System;
using System.Collections.Generic;
using System.Text;

namespace TransactionProcessor.IntegrationTests.Common
{
    using System.Data;
    using System.IO;
    using System.Net;
    using System.Threading;
    using Ductus.FluentDocker.Builders;
    using Ductus.FluentDocker.Services;
    using Ductus.FluentDocker.Services.Extensions;
    using Microsoft.Data.SqlClient;
    using Shouldly;
    using TechTalk.SpecFlow;

    [Binding]
    public class Setup
    {
        public static IContainerService DatabaseServerContainer;
        private static String DbConnectionStringWithNoDatabase;
        public static INetworkService DatabaseServerNetwork;

        [BeforeTestRun]
        protected static void GlobalSetup()
        {
            ShouldlyConfiguration.DefaultTaskTimeout = TimeSpan.FromMinutes(1);

            // Setup a network for the DB Server
            DatabaseServerNetwork = new Builder().UseNetwork($"sharednetwork").ReuseIfExist().Build();

            // Start the Database Server here
            DbConnectionStringWithNoDatabase = StartMySqlContainerWithOpenConnection();
        }

        public static String GetConnectionString(String databaseName)
        {
            return $"{DbConnectionStringWithNoDatabase} database={databaseName};";
        }

        private static String StartMySqlContainerWithOpenConnection()
        {
            String containerName = $"shareddatabasesqlserver";
            DatabaseServerContainer = new Ductus.FluentDocker.Builders.Builder()
                .UseContainer()
                .WithName(containerName)
                .WithCredential("https://docker.io", "stuartferguson", "Sc0tland")
                .UseImage("stuartferguson/subscriptionservicedatabasesqlserver")
                .WithEnvironment("ACCEPT_EULA=Y", $"SA_PASSWORD=thisisalongpassword123!")
                .ExposePort(1433)
                .UseNetwork(DatabaseServerNetwork)
                .KeepContainer()
                .KeepRunning()
                .ReuseIfExists()
                .Build()
                .Start()
                .WaitForPort("1433/tcp", 30000);

            IPEndPoint sqlServerEndpoint = DatabaseServerContainer.ToHostExposedEndpoint("1433/tcp");

            // Try opening a connection
            Int32 maxRetries = 10;
            Int32 counter = 1;

            String server = "127.0.0.1";
            String database = "SubscriptionServiceConfiguration";
            String user = "sa";
            String password = "thisisalongpassword123!";
            String port = sqlServerEndpoint.Port.ToString();

            String connectionString = $"server={server},{port};user id={user}; password={password}; database={database};";

            SqlConnection connection = new SqlConnection(connectionString);

            using (StreamWriter sw = new StreamWriter("C:\\Temp\\testlog.log", true))
            {
                while (counter <= maxRetries)
                {
                    try
                    {
                        sw.WriteLine($"Attempt {counter}");
                        sw.WriteLine(DateTime.Now);

                        connection.Open();

                        SqlCommand command = connection.CreateCommand();
                        command.CommandText = "SELECT * FROM EventStoreServers";
                        command.ExecuteNonQuery();

                        sw.WriteLine("Connection Opened");

                        connection.Close();

                        break;
                    }
                    catch (SqlException ex)
                    {
                        if (connection.State == ConnectionState.Open)
                        {
                            connection.Close();
                        }

                        sw.WriteLine(ex);
                        Thread.Sleep(20000);
                    }
                    finally
                    {
                        counter++;
                    }
                }
            }

            return $"server={containerName};user id={user}; password={password};";
        }
    }
}
