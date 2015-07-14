﻿using OpenChain.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace OpenChain.Ledger
{
    public class BasicValidator : IRulesValidator
    {
        private readonly ILedgerQueries queries;
        private readonly ITransactionStore store;

        public BasicValidator(ITransactionStore store, ILedgerQueries queries)
        {
            this.store = store;
            this.queries = queries;
        }

        public async Task ValidateAccountMutations(IReadOnlyList<AccountStatus> accountMutations, IReadOnlyList<SignatureEvidence> authentication)
        {
            IReadOnlyDictionary<AccountKey, AccountStatus> accounts =
                await this.store.GetAccounts(accountMutations.Select(entry => entry.AccountKey));

            foreach (AccountStatus account in accountMutations)
            {
                if (account.Version.Equals(BinaryData.Empty))
                    if (!await CheckCanCreate(authentication, account.AccountKey, accounts[account.AccountKey], account))
                        throw new TransactionInvalidException("AccountCannotBeCreated");

                if (account.Balance > 0)
                {
                    if (!CheckCanReceive(authentication, account.AccountKey, accounts[account.AccountKey], account))
                        throw new TransactionInvalidException("AccountCannotReceive");
                }
                else if (account.Balance < 0)
                {
                    if (!CheckCanSend(authentication, account.AccountKey, accounts[account.AccountKey], account))
                        throw new TransactionInvalidException("AccountCannotSend");
                }
            }
        }

        private bool CheckCanSend(IReadOnlyList<SignatureEvidence> authentication, AccountKey accountKey, AccountStatus currentState, AccountStatus proposedChange)
        {
            if (currentState.Balance + proposedChange.Balance < 0)
                return false;
            else
                return true;
        }

        private bool CheckCanReceive(IReadOnlyList<SignatureEvidence> authentication, AccountKey accountKey, AccountStatus currentState, AccountStatus proposedChange)
        {
            return !accountKey.Account.IsDirectory;
        }

        private async Task<bool> CheckCanCreate(IReadOnlyList<SignatureEvidence> authentication, AccountKey accountKey, AccountStatus currentState, AccountStatus proposedChange)
        {
            if (accountKey.Account.Segments.Count < 3)
                return false;

            if (accountKey.Account.Segments[0] != "account" || accountKey.Account.Segments[1] != "p2pkh")
                return false;

            LedgerPath rootPath = LedgerPath.FromSegments(new[] { accountKey.Account.Segments[0], accountKey.Account.Segments[1], accountKey.Account.Segments[2] }, true);

            AccountStatus parentAccount = (await this.store.GetAccounts(new[] { new AccountKey(rootPath.FullPath, currentState.AccountKey.Asset.FullPath) })).First().Value;
            if (parentAccount.Version.Equals(BinaryData.Empty))
                return false;

            return true;
        }

        public Task ValidateAssetDefinitionMutations(IReadOnlyList<KeyValuePair<LedgerPath, string>> assetDefinitionMutations, IReadOnlyList<SignatureEvidence> authentication)
        {
            return Task.FromResult(0);
        }
    }
}
