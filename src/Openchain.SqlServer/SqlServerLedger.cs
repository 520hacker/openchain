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
using System.Threading.Tasks;
using Openchain.Ledger;

namespace Openchain.SqlServer
{
    public class SqlServerLedger : SqlServerStorageEngine, ILedgerQueries, ILedgerIndexes
    {
        private readonly int instanceId;

        public SqlServerLedger(string connectionString, int instanceId, TimeSpan commandTimeout)
            : base(connectionString, instanceId, commandTimeout)
        {
            this.instanceId = instanceId;
        }

        public async Task<IReadOnlyList<Record>> GetKeyStartingFrom(ByteString prefix)
        {
            return await ExecuteQuery<Record>(
                "EXEC [Openchain].[GetRecordsFromKeyPrefix] @instance, @prefix;",
                reader => new Record(new ByteString((byte[])reader[0]), new ByteString((byte[])reader[1]), new ByteString((byte[])reader[2])),
                new Dictionary<string, object>()
                {
                    ["instance"] = this.instanceId,
                    ["prefix"] = prefix.ToByteArray()
                });
        }

        public async Task<IReadOnlyList<ByteString>> GetRecordMutations(ByteString recordKey)
        {
            return await ExecuteQuery<ByteString>(
                "EXEC [Openchain].[GetRecordMutations] @instance, @recordKey;",
                reader => new ByteString((byte[])reader[0]),
                new Dictionary<string, object>()
                {
                    ["instance"] = this.instanceId,
                    ["recordKey"] = recordKey.ToByteArray()
                });
        }

        public Task<ByteString> GetTransaction(ByteString mutationHash)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<Record>> GetAllRecords(RecordType type, string name)
        {
            throw new NotImplementedException();
        }
    }
}
