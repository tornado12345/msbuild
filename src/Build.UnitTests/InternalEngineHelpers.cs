﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using SdkResolverContext = Microsoft.Build.Framework.SdkResolverContext;
using SdkResult = Microsoft.Build.BackEnd.SdkResolution.SdkResult;
using SdkResultFactory = Microsoft.Build.Framework.SdkResultFactory;

namespace Microsoft.Build.Unittest
{
    internal static class SdkUtilities
    {
        public static ProjectOptions CreateProjectOptionsWithResolverFileMapping(Dictionary<string, string> mapping)
        {
            var resolver = new FileBasedMockSdkResolver(mapping);

            var context = EvaluationContext.Create(EvaluationContext.SharingPolicy.Isolated);
            var sdkService = (SdkResolverService)context.SdkResolverService;
            sdkService.InitializeForTests(null, new List<SdkResolver>(){resolver});

            return new ProjectOptions
            {
                EvaluationContext = context
            };
        }

        internal class ConfigurableMockSdkResolver : SdkResolver
        {
            private readonly Dictionary<string, SdkResult> _resultMap;

            public ConcurrentDictionary<string, int> ResolvedCalls { get; } = new ConcurrentDictionary<string, int>();

            public ConfigurableMockSdkResolver(SdkResult result)
            {
                _resultMap = new Dictionary<string, SdkResult> { [result.Sdk.Name] = result };
            }

            public ConfigurableMockSdkResolver(Dictionary<string, SdkResult> resultMap)
            {
                _resultMap = resultMap;
            }

            public override string Name => nameof(ConfigurableMockSdkResolver);

            public override int Priority => int.MaxValue;

            public override Framework.SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
            {
                ResolvedCalls.AddOrUpdate(sdkReference.Name, k => 1, (k, c) => c + 1);

                return _resultMap.TryGetValue(sdkReference.Name, out var result)
                    ? new SdkResult(sdkReference, result.Path, result.Version, null)
                    : null;
            }
        }

        internal class FileBasedMockSdkResolver : SdkResolver
        {
            private readonly Dictionary<string, string> _mapping;

            public FileBasedMockSdkResolver(Dictionary<string, string> mapping)
            {
                _mapping = mapping;
            }
            public override string Name => "FileBasedMockSdkResolver";
            public override int Priority => int.MinValue;

            public override Framework.SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
            {
                resolverContext.Logger.LogMessage($"{nameof(resolverContext.ProjectFilePath)} = {resolverContext.ProjectFilePath}", MessageImportance.High);
                resolverContext.Logger.LogMessage($"{nameof(resolverContext.SolutionFilePath)} = {resolverContext.SolutionFilePath}", MessageImportance.High);
                resolverContext.Logger.LogMessage($"{nameof(resolverContext.MSBuildVersion)} = {resolverContext.MSBuildVersion}", MessageImportance.High);

                return _mapping.ContainsKey(sdkReference.Name)
                    ? factory.IndicateSuccess(_mapping[sdkReference.Name], null)
                    : factory.IndicateFailure(new[] { $"Not in {nameof(_mapping)}" });
            }
        }
    }
}
