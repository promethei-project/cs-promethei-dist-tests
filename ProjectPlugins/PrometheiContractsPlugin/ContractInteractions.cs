using BlockchainUtils;
using PrometheiContractsPlugin.Marketplace;
using GethPlugin;
using Logging;
using System.Numerics;
using Utils;

namespace PrometheiContractsPlugin
{
    public class ContractInteractions
    {
        private readonly ILog log;
        private readonly IGethNode gethNode;

        public ContractInteractions(ILog log, IGethNode gethNode)
        {
            this.log = log;
            this.gethNode = gethNode;
        }

        public ContractAddress GetTokenAddress(ContractAddress marketplaceAddress)
        {
            log.Debug(marketplaceAddress.Address);
            var function = new TokenFunctionBase();
            var result = gethNode.Call<TokenFunctionBase, string>(marketplaceAddress, function);
            return new ContractAddress(result);
        }

        public string GetTokenName(ContractAddress tokenAddress)
        {
            try
            {
                log.Debug(tokenAddress.Address);
                var function = new NameFunction();

                return gethNode.Call<NameFunction, string>(tokenAddress, function);
            }
            catch (Exception ex)
            {
                log.Log("Failed to get token name: " + ex);
                return string.Empty;
            }
        }

        public string MintTestTokens(EthAddress address, BigInteger amount, ContractAddress tokenAddress)
        {
            log.Debug($"{amount} -> {address} (token: {tokenAddress})");
            return MintTokens(address.Address, amount, tokenAddress);
        }

        public decimal GetBalance(ContractAddress tokenAddress, string account)
        {
            log.Debug($"({tokenAddress}) {account}");
            var function = new BalanceOfFunction
            {
                Account = account
            };

            return gethNode.Call<BalanceOfFunction, BigInteger>(tokenAddress, function).ToDecimal();
        }

        public string TransferTestTokens(ContractAddress tokenAddress, string toAccount, BigInteger amount)
        {
            log.Debug($"({tokenAddress}) {toAccount} {amount}");
            var function = new TransferFunction
            {
                To = toAccount,
                Value = amount
            };

            return gethNode.SendTransaction(tokenAddress, function);
        }

        public bool IsSynced(ContractAddress marketplaceAddress, string marketplaceAbi)
        {
            log.Debug();
            try
            {
                return IsBlockNumberOK() && IsContractAvailable(marketplaceAddress, marketplaceAbi);
            }
            catch
            {
                return false;
            }
        }

        private string MintTokens(string account, BigInteger amount, ContractAddress tokenAddress)
        {
            log.Debug($"({tokenAddress}) {amount} --> {account}");
            if (string.IsNullOrEmpty(account)) throw new ArgumentException("Invalid arguments for MintTestTokens");
            if (amount == 0) return string.Empty;

            var function = new MintFunction
            {
                Holder = account,
                Amount = amount
            };

            return gethNode.SendTransaction(tokenAddress, function);
        }

        public void ApproveTestTokens(ContractAddress tokenAddress, EthAddress account, TestToken amount)
        {
            log.Debug($"({tokenAddress}) {account} approves {amount}");

            var function = new ApproveFunction
            {
                Spender = account.Address,
                Value = amount.TstWei
            };

            gethNode.SendTransaction(tokenAddress, function);
        }

        private bool IsBlockNumberOK()
        {
            var n = gethNode.GetSyncedBlockNumber();
            return n != null && n > 256;
        }

        private bool IsContractAvailable(ContractAddress marketplaceAddress, string marketplaceAbi)
        {
            return gethNode.IsContractAvailable(marketplaceAbi, marketplaceAddress);
        }
    }
}
