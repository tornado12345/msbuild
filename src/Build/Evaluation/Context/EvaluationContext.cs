﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>

using System;
using System.Threading;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation.Context
{
    /// <summary>
    ///     An object used by the caller to extend the lifespan of evaluation caches (by passing the object on to other
    ///     evaluations).
    ///     The caller should throw away the context when the environment changes (IO, environment variables, SDK resolution
    ///     inputs, etc).
    ///     This class and it's closure needs to be thread safe since API users can do evaluations in parallel.
    /// </summary>
    public class EvaluationContext
    {
        public enum SharingPolicy
        {
            Shared,
            Isolated
        }

        internal static Action<EvaluationContext> TestOnlyAlterStateOnCreate { get; set; }

        private int _used;

        internal SharingPolicy Policy { get; }

        internal virtual ISdkResolverService SdkResolverService { get; } = new CachingSdkResolverService();

        internal EvaluationContext(SharingPolicy policy)
        {
            Policy = policy;
        }

        /// <summary>
        ///     Factory for <see cref="EvaluationContext" />
        /// </summary>
        public static EvaluationContext Create(SharingPolicy policy)
        {
            var context = new EvaluationContext(policy);
            TestOnlyAlterStateOnCreate?.Invoke(context);

            return context;
        }

        internal EvaluationContext ContextForNewProject()
        {
            // Projects using isolated contexts need to get a new context instance 
            switch (Policy)
            {
                case SharingPolicy.Shared:
                    return this;
                case SharingPolicy.Isolated:
                    // use the first isolated context
                    var used = Interlocked.CompareExchange(ref _used, 1, 0);
                    return used == 0
                        ? this
                        : Create(Policy);
                default:
                    ErrorUtilities.ThrowInternalErrorUnreachable();
                    return null;
            }
        }
    }
}
