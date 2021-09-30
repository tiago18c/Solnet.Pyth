using Solnet.Pyth.Models;
using Solnet.Rpc;
using Solnet.Rpc.Core.Http;
using Solnet.Rpc.Core.Sockets;
using Solnet.Rpc.Messages;
using Solnet.Rpc.Models;
using Solnet.Rpc.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Solnet.Pyth
{
    /// <summary>
    /// Implements the base client
    /// </summary>
    public abstract class BaseClient
    {
        /// <summary>
        /// The RPC client.
        /// </summary>
        protected IRpcClient RpcClient { get; init; }

        /// <summary>
        /// The streaming RPC client.
        /// </summary>
        protected IStreamingRpcClient StreamingRpcClient { get; init; }

        /// <summary>
        /// Initialize the base client with the given RPC clients.
        /// </summary>
        /// <param name="rpcClient">The RPC client instance.</param>
        /// <param name="streamingRpcClient">The streaming RPC client instance.</param>
        protected BaseClient(IRpcClient rpcClient, IStreamingRpcClient streamingRpcClient)
        {
            RpcClient = rpcClient;
            StreamingRpcClient = streamingRpcClient;
        }

        /// <summary>
        /// Deserializes the given byte array into the specified type.
        /// </summary>
        /// <param name="data">The data to deserialize into the specified type.</param>
        /// <typeparam name="T">The type.</typeparam>
        /// <returns>An instance of the specified type or null in case it was unable to deserialize.</returns>
        private static T DeserializeAccount<T>(byte[] data) where T : class
        {
            System.Reflection.MethodInfo m = typeof(T).GetMethod("Deserialize",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(byte[]) }, null);

            if (m == null)
                return null;
            return (T)m.Invoke(null, new object[] { data });
        }

        /// <summary>
        /// Gets the account info for the given account address and attempts to deserialize the account data into the specified type.
        /// </summary>
        /// <param name="accountAddresses">The list of account addresses to fetch.</param>
        /// <param name="commitment">The commitment parameter for the RPC request.</param>
        /// <typeparam name="T">The specified type.</typeparam>
        /// <returns>A <see cref="ResultWrapper{T, T2}"/> containing the RPC response and the deserialized account if successful.</returns>
        protected async Task<MultipleAccountsResultWrapper<List<T>>> GetMultipleAccounts<T>(List<string> accountAddresses,
            Commitment commitment = Commitment.Finalized) where T : class
        {
            RequestResult<ResponseValue<List<AccountInfo>>> res =
                await RpcClient.GetMultipleAccountsAsync(accountAddresses, commitment);
            
            if (!res.WasSuccessful || !(res.Result?.Value?.Count > 0))
                return new MultipleAccountsResultWrapper<List<T>>(res);

            List<T> resultingAccounts = new(res.Result.Value.Count);
            resultingAccounts.AddRange(res.Result.Value.Select(result =>
                DeserializeAccount<T>(Convert.FromBase64String(result.Data[0]))));
            
            return new MultipleAccountsResultWrapper<List<T>>(res, resultingAccounts);
        }

        /// <summary>
        /// Gets the account info for the given account address and attempts to deserialize the account data into the specified type.
        /// </summary>
        /// <param name="accountAddress">The account address.</param>
        /// <param name="commitment">The commitment parameter for the RPC request.</param>
        /// <typeparam name="T">The specified type.</typeparam>
        /// <returns>A <see cref="ResultWrapper{T, T2}"/> containing the RPC response and the deserialized account if successful.</returns>
        protected async Task<AccountResultWrapper<T>> GetAccount<T>(string accountAddress,
            Commitment commitment = Commitment.Finalized) where T : class
        {
            RequestResult<ResponseValue<AccountInfo>> res =
                await RpcClient.GetAccountInfoAsync(accountAddress, commitment);

            if (res.WasSuccessful && res.Result?.Value.Data?.Count > 0)
            {
                return new AccountResultWrapper<T>(res,
                    DeserializeAccount<T>(Convert.FromBase64String(res.Result.Value.Data[0])));
            }

            return new AccountResultWrapper<T>(res);
        }

        /// <summary>
        /// Subscribes to notifications on changes to the given account and deserializes the account data into the specified type.
        /// </summary>
        /// <param name="accountAddress">The account address.</param>
        /// <param name="commitment">The commitment parameter for the RPC request.</param>
        /// <param name="callback">An action that is called when a notification is received</param>
        /// <typeparam name="T">The specified type.</typeparam>
        /// <returns>The subscription state.</returns>
        protected async Task<SubscriptionState> SubscribeAccount<T>(string accountAddress,
            Action<SubscriptionState, ResponseValue<AccountInfo>, T> callback,
            Commitment commitment = Commitment.Finalized) where T : class
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress,
                (s, e) =>
                {
                    T parsingResult = null;

                    if (e.Value?.Data?.Count > 0)
                        parsingResult = DeserializeAccount<T>(Convert.FromBase64String(e.Value.Data[0]));

                    callback(s, e, parsingResult);
                }, commitment);

            return res;
        }
    }
}