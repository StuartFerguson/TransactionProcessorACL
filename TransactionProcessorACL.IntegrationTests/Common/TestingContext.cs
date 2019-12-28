﻿using System;
using System.Collections.Generic;
using System.Text;

namespace TransactionProcessor.IntegrationTests.Common
{
    using System.Linq;
    using DataTransferObjects;
    using global::Shared.Logger;
    using Shouldly;
    using TechTalk.SpecFlow;

    public class TestingContext
    {
        public TestingContext()
        {
            this.Estates = new List<EstateDetails>();
            this.Clients = new List<ClientDetails>();
        }

        public NlogLogger Logger { get; set; }

        public DockerHelper DockerHelper { get; set; }

        private List<ClientDetails> Clients;

        private List<EstateDetails> Estates;

        public String AccessToken { get; set; }

        public EstateDetails GetEstateDetails(TableRow tableRow)
        {
            String estateName = SpecflowTableHelper.GetStringRowValue(tableRow, "EstateName");

            EstateDetails estateDetails = this.Estates.SingleOrDefault(e => e.EstateName == estateName);

            estateDetails.ShouldNotBeNull();

            return estateDetails;
        }

        public EstateDetails GetEstateDetails(String estateName)
        {
            EstateDetails estateDetails = this.Estates.SingleOrDefault(e => e.EstateName == estateName);

            estateDetails.ShouldNotBeNull();

            return estateDetails;
        }

        public EstateDetails GetEstateDetails(Guid estateId)
        {
            EstateDetails estateDetails = this.Estates.SingleOrDefault(e => e.EstateId == estateId);

            estateDetails.ShouldNotBeNull();

            return estateDetails;
        }

        public void AddEstateDetails(Guid estateId, String estateName)
        {
            this.Estates.Add(EstateDetails.Create(estateId, estateName));
        }

        public void AddClientDetails(String clientId,
                                     String clientSecret,
                                     String grantType)
        {
            this.Clients.Add(ClientDetails.Create(clientId, clientSecret, grantType));
        }

        public ClientDetails GetClientDetails(String clientId)
        {
            ClientDetails clientDetails = this.Clients.SingleOrDefault(c => c.ClientId == clientId);

            clientDetails.ShouldNotBeNull();

            return clientDetails;
        }
    }

    public class EstateDetails
    {
        private EstateDetails(Guid estateId, String estateName)
        {
            this.EstateId = estateId;
            this.EstateName = estateName;
            this.Merchants = new Dictionary<String, Guid>();
            this.Operators = new Dictionary<String, Guid>();
            this.MerchantUsers = new Dictionary<String, Dictionary<String, String>>();
            this.MerchantUsersTokens = new Dictionary<String, Dictionary<String, String>>();
            this.TransactionResponses = new Dictionary<(Guid merchantId, String transactionNumber, String transactionType), String>();
        }

        public void AddTransactionResponse(Guid merchantId,
                                           String transactionNumber,
                                           String transactionType,
                                           String transactionResponse)
        {
            this.TransactionResponses.Add((merchantId, transactionNumber,transactionType), transactionResponse);
        }

        public String GetTransactionResponse(Guid merchantId,
                                                        String transactionNumber,
                                                        String transactionType)
        {
            KeyValuePair<(Guid merchantId, String transactionNumber, String transactionType), String> transactionResponse =
                this.TransactionResponses.Where(t => t.Key.merchantId == merchantId && t.Key.transactionNumber == transactionNumber && t.Key.transactionType == transactionType).SingleOrDefault();

            return transactionResponse.Value;
        }

        private Dictionary<(Guid merchantId, String transactionNumber, String transactionType), String> TransactionResponses { get; set; }

        public String EstateUser { get; private set; }
        public String EstatePassword { get; private set; }

        public String AccessToken { get; private set; }

        public static EstateDetails Create(Guid estateId,
                                           String estateName)
        {
            return new EstateDetails(estateId, estateName);
        }

        public void AddOperator(Guid operatorId,
                                String operatorName)
        {
            this.Operators.Add(operatorName, operatorId);
        }

        public void AddMerchant(Guid merchantId,
                                String merchantName)
        {
            this.Merchants.Add(merchantName, merchantId);
        }

        public Guid GetMerchantId(String merchantName)
        {
            return this.Merchants.Single(m => m.Key == merchantName).Value;
        }

        public Guid GetOperatorId(String operatorName)
        {
            return this.Operators.Single(o => o.Key == operatorName).Value;
        }

        public void SetEstateUser(String userName,
                                  String password)
        {
            this.EstateUser = userName;
            this.EstatePassword = password;
        }

        public void AddMerchantUser(String merchantName,
                                    String userName,
                                    String password)
        {
            if (this.MerchantUsers.ContainsKey(merchantName))
            {
                Dictionary<String, String> merchantUsersList = this.MerchantUsers[merchantName];
                if (merchantUsersList.ContainsKey(userName) == false)
                {
                    merchantUsersList.Add(userName, password);
                }
            }
            else
            {
                Dictionary<String, String> merchantUsersList = new Dictionary<String, String>();
                merchantUsersList.Add(userName, password);
                this.MerchantUsers.Add(merchantName, merchantUsersList);
            }
        }

        public void AddMerchantUserToken(String merchantName,
                                    String userName,
                                    String token)
        {
            if (this.MerchantUsersTokens.ContainsKey(merchantName))
            {
                Dictionary<String, String> merchantUsersList = this.MerchantUsersTokens[merchantName];
                if (merchantUsersList.ContainsKey(userName) == false)
                {
                    merchantUsersList.Add(userName, token);
                }
            }
            else
            {
                Dictionary<String, String> merchantUsersList = new Dictionary<String, String>();
                merchantUsersList.Add(userName, token);
                this.MerchantUsersTokens.Add(merchantName, merchantUsersList);
            }
        }

        public String GetMerchantUserToken(String merchantName)
        {
            KeyValuePair<String, Dictionary<String, String>> x = this.MerchantUsersTokens.SingleOrDefault(x => x.Key == merchantName);

            if (x.Value != null)
            {
                return x.Value.First().Value;
            }

            return String.Empty;
        }
        public void SetEstateUserToken(String accessToken)
        {
            this.AccessToken = accessToken;
        }

        public Guid EstateId { get; private set; }
        public String EstateName { get; private set; }

        private Dictionary<String, Guid> Operators;

        private Dictionary<String, Guid> Merchants;

        private Dictionary<String, Dictionary<String, String>> MerchantUsers;
        private Dictionary<String, Dictionary<String, String>> MerchantUsersTokens;
    }

    public class ClientDetails
    {
        public String ClientId { get; private set; }
        public String ClientSecret { get; private set; }
        public String GrantType { get; private set; }

        private ClientDetails(String clientId,
                              String clientSecret,
                              String grantType)
        {
            this.ClientId = clientId;
            this.ClientSecret = clientSecret;
            this.GrantType = grantType;
        }

        public static ClientDetails Create(String clientId,
                                           String clientSecret,
                                           String grantType)
        {
            return new ClientDetails(clientId, clientSecret, grantType);
        }
    }
}
