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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Openchain.Infrastructure
{
    public class TransactionValidator
    {
        private static readonly int MaxKeySize = 512;

        private readonly IStorageEngine store;
        private readonly HashSet<ByteString> validNamespaces;
        private readonly IMutationValidator validator;

        public TransactionValidator(IStorageEngine store, IMutationValidator validator, IEnumerable<string> rootUrls)
        {
            this.store = store;
            this.validator = validator;
            this.validNamespaces = new HashSet<ByteString>(rootUrls.Select(url => new ByteString(Encoding.UTF8.GetBytes(url))));
        }

        public async Task<ByteString> PostTransaction(ByteString rawMutation, IReadOnlyList<SignatureEvidence> authentication)
        {
            Mutation mutation;
            try
            {
                // Verify that the mutation can be deserialized
                mutation = MessageSerializer.DeserializeMutation(rawMutation);
            }
            catch (InvalidProtocolBufferException)
            {
                throw new TransactionInvalidException("InvalidMutation");
            }

            if (!this.validNamespaces.Contains(mutation.Namespace))
                throw new TransactionInvalidException("InvalidNamespace");

            if (mutation.Records.Count == 0)
                throw new TransactionInvalidException("InvalidMutation");

            if (mutation.Records.Any(record => record.Key.Value.Count > MaxKeySize))
                throw new TransactionInvalidException("InvalidMutation");

            ValidateAuthentication(authentication, MessageSerializer.ComputeHash(rawMutation.ToByteArray()));

            ParsedMutation parsedMutation = ParsedMutation.Parse(mutation);

            // All assets must have an overall zero balance

            IReadOnlyDictionary<AccountKey, AccountStatus> accounts =
                await this.store.GetAccounts(parsedMutation.AccountMutations.Select(entry => entry.AccountKey));

            var groups = parsedMutation.AccountMutations
                .GroupBy(account => account.AccountKey.Asset.FullPath)
                .Select(group => group.Sum(entry => entry.Balance - accounts[entry.AccountKey].Balance));

            if (groups.Any(group => group != 0))
                throw new TransactionInvalidException("UnbalancedTransaction");

            DateTime date = DateTime.UtcNow;

            await this.validator.Validate(parsedMutation, authentication, accounts);

            TransactionMetadata metadata = new TransactionMetadata(authentication);

            byte[] rawMetadata = SerializeMetadata(metadata);

            Transaction transaction = new Transaction(rawMutation, date, new ByteString(rawMetadata));
            byte[] serializedTransaction = MessageSerializer.SerializeTransaction(transaction);

            try
            {
                await this.store.AddTransactions(new[] { new ByteString(serializedTransaction) });
            }
            catch (ConcurrentMutationException)
            {
                throw new TransactionInvalidException("OptimisticConcurrency");
            }

            return new ByteString(MessageSerializer.ComputeHash(serializedTransaction));
        }

        private static void ValidateAuthentication(IReadOnlyList<SignatureEvidence> authentication, byte[] mutationHash)
        {
            foreach (SignatureEvidence evidence in authentication)
            {
                if (!evidence.VerifySignature(mutationHash))
                    throw new TransactionInvalidException("InvalidSignature");
            }
        }

        private byte[] SerializeMetadata(TransactionMetadata metadata)
        {
            Messages.TransactionMetadata transactionMetadataBuilder = new Messages.TransactionMetadata();
            transactionMetadataBuilder.Signatures.Add(metadata.Signatures.Select(
                signature => new Messages.TransactionMetadata.Types.SignatureEvidence()
                {
                    PublicKey = Google.Protobuf.ByteString.Unsafe.FromBytes(signature.PublicKey.ToByteArray()),
                    Signature = Google.Protobuf.ByteString.Unsafe.FromBytes(signature.Signature.ToByteArray())
                }));

            return transactionMetadataBuilder.ToByteArray();
        }
    }
}
