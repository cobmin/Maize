﻿using JsonFlatten;
using LoopMintSharp.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoopMintSharp
{
    public class LoopringMintService : ILoopringMintService, IDisposable
    {
        readonly RestClient _client;

        public LoopringMintService(string baseUrl)
        {
            _client = new RestClient(baseUrl);
        }

        public async Task<StorageId> GetNextStorageId(string apiKey, int accountId, int sellTokenId)
        {
            var request = new RestRequest("api/v3/storageId");
            request.AddHeader("x-api-key", apiKey);
            request.AddParameter("accountId", accountId);
            request.AddParameter("sellTokenId", sellTokenId);
            try
            {
                var response = await _client.GetAsync(request);
                var data = JsonConvert.DeserializeObject<StorageId>(response.Content!);
                return data;
            }
            catch (HttpRequestException httpException)
            {
                return null;
            }
        }

        public async Task<CounterFactualNft> ComputeTokenAddress(string apiKey, CounterFactualNftInfo counterFactualNftInfo)
        {
            var request = new RestRequest("api/v3/nft/info/computeTokenAddress");
            request.AddHeader("x-api-key", apiKey);
            request.AddParameter("nftFactory", counterFactualNftInfo.nftFactory);
            request.AddParameter("nftOwner", counterFactualNftInfo.nftOwner);
            request.AddParameter("nftBaseUri", counterFactualNftInfo.nftBaseUri);
            try
            {
                var response = await _client.GetAsync(request);
                var data = JsonConvert.DeserializeObject<CounterFactualNft>(response.Content!);
                return data;
            }
            catch (HttpRequestException httpException)
            {
                return null;
            }
        }

        public async Task<OffchainFee> GetOffChainFee(string apiKey, int accountId, int requestType, string tokenAddress)
        {
            var request = new RestRequest("api/v3/user/nft/offchainFee");
            request.AddHeader("x-api-key", apiKey);
            request.AddParameter("accountId", accountId);
            request.AddParameter("requestType", requestType);
            request.AddParameter("tokenAddress", tokenAddress);
            try
            {
                var response = await _client.GetAsync(request);
                var data = JsonConvert.DeserializeObject<OffchainFee>(response.Content!);
                return data;
            }
            catch (HttpRequestException httpException)
            {
                Console.WriteLine($"Error getting off chain fee: {httpException.Message}");
                return null;
            }
        }

        public async Task<OffchainFee> GetOffChainFeeWithAmount(string apiKey, int accountId, int amount, int requestType, string tokenAddress)
        {
            var request = new RestRequest("api/v3/user/nft/offchainFee");
            request.AddHeader("x-api-key", apiKey);
            request.AddParameter("accountId", accountId);
            request.AddParameter("amount", amount); 
            request.AddParameter("requestType", requestType);
            request.AddParameter("tokenAddress", "0xbeb2f2367c1e79003dffa34f16a2b933624a6e05");
            try
            {
                var response = await _client.GetAsync(request);
                var data = JsonConvert.DeserializeObject<OffchainFee>(response.Content!);
                return data;
            }
            catch (HttpRequestException httpException)
            {
                Console.WriteLine($"Error getting off chain fee: {httpException.Message}");
                return null;
            }
        }

        public async Task<MintResponseData> MintNft(
            string apiKey, 
            string exchange, 
            int minterId, 
            string minterAddress,
            int toAccountId,
            string toAddress, 
            int nftType, 
            string tokenAddress, 
            string nftId, 
            string amount, 
            long validUntil, 
            int royaltyPercentage, 
            int storageId, 
            int maxFeeTokenId, 
            string maxFeeAmount, 
            bool forceToMint, 
            CounterFactualNftInfo counterFactualNftInfo,
            string eddsaSignature, 
            string royaltyAddress)
        {
            var request = new RestRequest("api/v3/nft/mint");
            request.AddHeader("x-api-key", apiKey);
            request.AlwaysMultipartFormData = true;
            request.AddParameter("exchange", exchange);
            request.AddParameter("minterId", minterId);
            request.AddParameter("minterAddress", minterAddress);
            request.AddParameter("toAccountId", toAccountId);
            request.AddParameter("toAddress", toAddress);
            request.AddParameter("nftType", nftType);
            request.AddParameter("tokenAddress", tokenAddress);
            request.AddParameter("nftId", nftId);
            request.AddParameter("amount", amount);
            request.AddParameter("validUntil", validUntil);
            request.AddParameter("royaltyPercentage", royaltyPercentage);
            request.AddParameter("storageId", storageId);
            request.AddParameter("maxFee.tokenId", maxFeeTokenId);
            request.AddParameter("maxFee.amount", maxFeeAmount);
            request.AddParameter("forceToMint", "false");
            request.AddParameter("royaltyAddress", royaltyAddress);
            request.AddParameter("counterFactualNftInfo.nftFactory", counterFactualNftInfo.nftFactory);
            request.AddParameter("counterFactualNftInfo.nftOwner", counterFactualNftInfo.nftOwner);
            request.AddParameter("counterFactualNftInfo.nftBaseUri", counterFactualNftInfo.nftBaseUri);
            request.AddParameter("eddsaSignature", eddsaSignature);

            try
            {
                var response = await _client.ExecutePostAsync(request);
                var data = JsonConvert.DeserializeObject<MintResponseData>(response.Content!);
                if(!response.IsSuccessful)
                {
                    data.errorMessage = response.Content;
                    Console.WriteLine($"Error minting nft: {response.Content}");
                }
                else if(!response.IsSuccessful)
                {
                    data.errorMessage = response.Content;
                }
                return data;
            }
            catch (HttpRequestException httpException)
            {
                var data = new MintResponseData();
                data.errorMessage = httpException.Message;
                return null;
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task<CreateCollectionResult> CreateNftCollection(
            string apiKey, 
            CreateCollectionRequest createCollectionRequest,
            string apiSig)
        {
            var request = new RestRequest("api/v3/nft/collection", Method.Post);
            request.AddHeader("x-api-key", apiKey);
            request.AddHeader("x-api-sig", apiSig);
            request.AddHeader("Accept", "application/json");
            var jObject = JObject.Parse(JsonConvert.SerializeObject(createCollectionRequest));
            var jObjectFlattened = jObject.Flatten();
            var jObjectFlattenedString = JsonConvert.SerializeObject(jObjectFlattened);
            request.AddParameter("application/json", jObjectFlattenedString, ParameterType.RequestBody);

            try
            {
                var response = await _client.ExecuteAsync(request);
                var data = JsonConvert.DeserializeObject<CreateCollectionResult>(response.Content!);
                if (!response.IsSuccessful)
                {
                    Console.WriteLine($"Error creating nft collection: {response.Content}");
                }
                else if (!response.IsSuccessful)
                {
                    Console.WriteLine($"Error creating nft collection: {response.Content}");
                }
                return data;
            }
            catch (HttpRequestException httpException)
            {
                var data = new MintResponseData();
                data.errorMessage = httpException.Message;
                return null;
            }
        }

        public async Task<CollectionResult> FindNftCollection(string apiKey, int limit, int offset, string owner, string tokenAddress)
        {
            var request = new RestRequest("api/v3/nft/collection");
            request.AddHeader("x-api-key", apiKey);
            request.AddParameter("limit", limit.ToString());
            request.AddParameter("offset", offset.ToString());
            request.AddParameter("owner", owner);
            request.AddParameter("tokenAddress", tokenAddress);
            try
            {
                var response = await _client.GetAsync(request);
                var data = JsonConvert.DeserializeObject<CollectionResult>(response.Content!);
                return data;
            }
            catch (HttpRequestException httpException)
            {
                return null;
            }
        }

        public async Task<RedPacketNftMintResponse> MintRedPacketNft(string apiKey, string apiSig, RedPacketNft redPacketNft)
        {
            var request = new RestRequest("/api/v3/luckyToken/sendLuckyToken", Method.Post);
            request.AddHeader("x-api-key", apiKey);
            request.AddHeader("x-api-sig", apiSig);
            request.AddHeader("Accept", "application/json");
            var jObject = JObject.Parse(JsonConvert.SerializeObject(redPacketNft));
            var jObjectFlattened = jObject.Flatten();
            var jObjectFlattenedString = JsonConvert.SerializeObject(jObjectFlattened);
            request.AddParameter("application/json", jObjectFlattenedString, ParameterType.RequestBody);
            try
            {
                var response = await _client.ExecuteAsync(request);
                var data = JsonConvert.DeserializeObject<RedPacketNftMintResponse>(response.Content!);
                data.nftData = redPacketNft.nftData;
                if (response.IsSuccessful)
                {
                    Console.WriteLine($"Red packet nft mint response: {data}");
                }
                else if (!response.IsSuccessful)
                {
                    Console.WriteLine($"Error creating minting red packet nft: {response.Content}");
                    data.errorMessage = response.Content;
                }
                else if (!response.IsSuccessful)
                {
                    Console.WriteLine($"Error creating minting red packet nft: {response.Content}");
                    data.errorMessage = response.Content;
                }
                return data;
            }
            catch (HttpRequestException httpException)
            {
                var data = new RedPacketNftMintResponse();
                data.errorMessage = httpException.Message;
                return data;
            }
        }

        public async Task<NftBalance> GetTokenIdWithCheck(string apiKey, int accountId, string nftData)
        {
            var data = new NftBalance();
            var request = new RestRequest("/api/v3/user/nft/balances");
            request.AddHeader("x-api-key", apiKey);
            request.AddParameter("accountId", accountId);
            request.AddParameter("nftDatas", nftData);
            request.AddParameter("metadata", "true");
            try
            {
                var response = await _client.GetAsync(request);
                data = JsonConvert.DeserializeObject<NftBalance>(response.Content!);
                return data;
            }
            catch (HttpRequestException httpException)
            {
                return null;
            }
        }
    }
}
