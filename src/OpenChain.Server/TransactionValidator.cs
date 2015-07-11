﻿using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using OpenChain.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenChain.Server
{
    public class TransactionValidator
    {
        private readonly ILedgerStore store;
        private readonly IRulesValidator validator;
        private readonly string ledgerId;

        public TransactionValidator(ILedgerStore store, IRulesValidator validator, string ledgerId)
        {
            this.store = store;
            this.validator = validator;
            this.ledgerId = ledgerId;
        }

        public async Task<BinaryData> PostTransaction(BinaryData rawTransaction, IReadOnlyList<AuthenticationEvidence> authentication)
        {
            // Verify that the transaction can be deserialized
            Transaction transaction = MessageSerializer.DeserializeTransaction(rawTransaction.ToByteArray());

            if (transaction.LedgerId != this.ledgerId)
                throw new TransactionInvalidException("InvalidLedgerId");

            // All assets must have an overall zero balance
            var groups = transaction.AccountEntries
                .GroupBy(entry => entry.AccountKey.Asset)
                .Select(group => group.Sum(entry => entry.Amount));

            if (groups.Any(group => group != 0))
                throw new TransactionInvalidException("UnbalancedTransaction");

            // There must not be the same account represented twice
            var accountEntries = transaction.AccountEntries
                .GroupBy(entry => entry.AccountKey, entry => entry);

            if (accountEntries.Any(group => group.Count() > 1))
                throw new TransactionInvalidException("DuplicateAccount");

            // Paths must be correctly formatted
            if (!transaction.AccountEntries.All(
                account => LedgerPath.IsValidPath(account.AccountKey.Account) && LedgerPath.IsValidPath(account.AccountKey.Asset)))
                throw new TransactionInvalidException("InvalidPath");

            DateTime date = DateTime.UtcNow;
            
            await this.validator.Validate(transaction, authentication);

            LedgerRecordMetadata recordMetadata = new LedgerRecordMetadata(1, authentication);

            byte[] metadata = BsonExtensionMethods.ToBson<LedgerRecordMetadata>(recordMetadata);

            LedgerRecord record = new LedgerRecord(rawTransaction, date, new BinaryData(metadata));
            byte[] serializedLedgerRecord = MessageSerializer.SerializeLedgerRecord(record);

            try
            {
                await this.store.AddLedgerRecords(new[] { new BinaryData(serializedLedgerRecord) });
            }
            catch (AccountModifiedException)
            {
                throw new TransactionInvalidException("OptimisticConcurrency");
            }

            return new BinaryData(MessageSerializer.ComputeHash(serializedLedgerRecord));
        }
    }
}
