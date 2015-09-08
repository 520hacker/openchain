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

using OpenChain.Ledger.Validation;
using Xunit;

namespace OpenChain.Ledger.Tests
{
    public class AclTests
    {
        private static readonly P2pkhSubject subject = new P2pkhSubject(new[] { "n12RA1iohYEerfXiBixSoERZG8TP8xQFL2" }, 1, new KeyEncoder(111));
        private static readonly SignatureEvidence[] evidence = new[] { new SignatureEvidence(ByteString.Parse("abcdef"), ByteString.Empty) };
        private static readonly LedgerPath path = LedgerPath.Parse("/root/subitem/");
        private static readonly PermissionSet permissions = PermissionSet.AllowAll;

        [Fact]
        public void IsMatch_Success()
        {
            // Recursive ACL
            Acl acl = new Acl(new[] { subject }, path, true, new StringPattern("name", PatternMatchingStrategy.Exact), permissions);
            // Match (recursiveOnly = true)
            Assert.True(acl.IsMatch(evidence, path, true, "name"));
            // Match (recursiveOnly = false)
            Assert.True(acl.IsMatch(evidence, path, false, "name"));

            // Non-recursive ACL
            acl = new Acl(new[] { subject }, path, false, new StringPattern("name", PatternMatchingStrategy.Exact), permissions);
            // Match (recursiveOnly = false)
            Assert.True(acl.IsMatch(evidence, path, false, "name"));
            // Error: record non recursive (recursiveOnly = true)
            Assert.False(acl.IsMatch(evidence, path, true, "name"));
            // Error: path mismatch
            Assert.False(acl.IsMatch(evidence, LedgerPath.Parse("/"), false, "name"));
            // Error: name mismatch
            Assert.False(acl.IsMatch(evidence, path, false, "n"));
            // Error: identity mismatch
            Assert.False(acl.IsMatch(new[] { new SignatureEvidence(ByteString.Parse("ab"), ByteString.Empty) }, path, false, "name"));
        }
    }
}
