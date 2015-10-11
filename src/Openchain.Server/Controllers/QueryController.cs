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

using Microsoft.AspNet.Cors.Core;
using Microsoft.AspNet.Mvc;
using Openchain.Ledger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Openchain.Server.Controllers
{
    [EnableCors("Any")]
    [Route("query")]
    public class QueryController : Controller
    {
        private readonly ILedgerQueries store;

        public QueryController(ILedgerQueries store)
        {
            this.store = store;
        }

        [HttpGet("account")]
        public async Task<ActionResult> GetAccount(string account)
        {
            IReadOnlyList<AccountStatus> accounts = await this.store.GetAccount(account);

            return Json(accounts.Select(GetAccountJson).ToArray());
        }

        [HttpGet("transaction")]
        public async Task<ActionResult> GetTransaction([FromQuery(Name = "mutation_hash")] string mutationHash)
        {
            ByteString parsedMutationHash;
            try
            {
                parsedMutationHash = ByteString.Parse(mutationHash);
            }
            catch (FormatException)
            {
                return HttpBadRequest();
            }

            ByteString transaction = await this.store.GetTransaction(parsedMutationHash);

            if (transaction == null)
                return new HttpStatusCodeResult(404);
            else
                return Json(new { raw = transaction.ToString() });
        }

        [HttpGet("subaccounts")]
        public async Task<ActionResult> GetSubaccounts(string account)
        {
            LedgerPath path;
            if (!LedgerPath.TryParse(account, out path))
                return HttpBadRequest();

            LedgerPath directory = LedgerPath.FromSegments(path.Segments.ToArray());

            IReadOnlyList<Record> accounts = await this.store.GetSubaccounts(directory.FullPath);

            return Json(accounts.Select(result => new
            {
                key = result.Key.ToString(),
                value = result.Value.ToString(),
                version = result.Version.ToString()
            }).ToArray());
        }

        private object GetAccountJson(AccountStatus account)
        {
            return new
            {
                account = account.AccountKey.Account.FullPath,
                asset = account.AccountKey.Asset.FullPath,
                balance = account.Balance.ToString(),
                version = account.Version.ToString()
            };
        }

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            base.OnActionExecuted(context);

            if (context.Exception is NotSupportedException)
            {
                context.Result = new HttpStatusCodeResult(501);
                context.ExceptionHandled = true;
            }
        }
    }
}
