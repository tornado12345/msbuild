// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class WeakStringCache_Tests : IDisposable
    {
        /// <summary>
        /// The weak string cache under test.
        /// </summary>
        private WeakStringCache _cache = new WeakStringCache();

        public void Dispose()
        {
            _cache.Dispose();
        }

        /// <summary>
        /// Adds a string to the cache under test.
        /// </summary>
        /// <param name="strPart1">Part one of the string (split to prevent runtime interning and unintended GC roots).</param>
        /// <param name="strPart2">Part two of the string (split to prevent runtime interning and unintended GC roots).</param>
        /// <param name="callbackToRunWithTheStringAlive">Callback to be invoked after the string has been added but before the strong GC ref is released.</param>
        /// <returns>The hash code of the string as calculated by WeakStringCache.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int AddString(string strPart1, string strPart2, Action<string> callbackToRunWithTheStringAlive)
        {
            // Compose the string with SB so it doesn't get interned by the runtime.
            string testString = new StringBuilder(strPart1).Append(strPart2).ToString();
            StringInternTarget testStringTarget = new StringInternTarget(testString);

            int hashCode = WeakStringCache.GetInternableHashCode(testStringTarget);

            string cachedString = _cache.GetOrCreateEntry(testStringTarget, out bool cacheHit);
            cacheHit.ShouldBeFalse();
            cachedString.ShouldBeSameAs(testString);

            callbackToRunWithTheStringAlive(cachedString);

            // Verify that the string is really in the cache and the cache returns the interned instance.
            string testStringCopy = new StringBuilder(strPart1).Append(strPart2).ToString();
            cachedString = _cache.GetOrCreateEntry(new StringInternTarget(testStringCopy), out cacheHit);
            cacheHit.ShouldBeTrue();
            cachedString.ShouldBeSameAs(testString);

            // Trigger full GC and verify that nothing has changed since we're still keeping testString alive.
            GC.Collect();

            callbackToRunWithTheStringAlive(cachedString);

            cachedString = _cache.GetOrCreateEntry(new StringInternTarget(testStringCopy), out cacheHit);
            cacheHit.ShouldBeTrue();
            cachedString.ShouldBeSameAs(testString);

            return hashCode;
        }

        /// <summary>
        /// Adds strings that are known to have a hash code collision to the cache under test.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AddStringsWithSameHashCode(int numberOfStrings)
        {
            string[] cachedStrings = new string[numberOfStrings];
            int[] hashCodes = new int[numberOfStrings];

            for (int i = 0; i < numberOfStrings; i++)
            {
                string strPart2 = "1" + String.Concat(Enumerable.Repeat("4428939786", i));
                hashCodes[i] = AddString("Random string ", strPart2, (string cachedString) =>
                {
                    _cache.GetDebugInfo().ShouldBe(new WeakStringCache.DebugInfo()
                    {
                        LiveStringCount = 1,
                        CollectedStringCount = 0,
                    });
                    cachedStrings[i] = cachedString;
                });

                if (i > 0)
                {
                    // The strings have been carefully constructed to have the same hash code.
                    hashCodes[i].ShouldBe(hashCodes[i - 1]);
                }
            }

            // There are no cache hits when iterating over our strings again because the last one always wins and steals the slot.
            for (int i = 0; i < numberOfStrings; i++)
            {
                StringBuilder sb = new StringBuilder(cachedStrings[i]);
                string cachedStringFromCache =_cache.GetOrCreateEntry(new StringBuilderInternTarget(sb), out bool cacheHit);
                cacheHit.ShouldBeFalse();
                cachedStringFromCache.ShouldNotBeSameAs(cachedStrings[i]);
            }
        }

        /// <summary>
        /// Simple test case to verify that:
        /// 1. A string added to the cache stays in the cache as long as it's alive.
        /// 2. The string is no longer retrievable after all strong GC refs are gone.
        /// 3. The cache completely removes the handle after calling Scavenge on it.
        /// </summary>
        /// <remarks>
        /// Disabled on MacOS Mono because it doesn't play well with conservative GC scanning.
        /// https://www.mono-project.com/docs/advanced/garbage-collector/sgen/#precise-stack-marking
        /// </remarks>
        [Fact]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Mono, "doesn't play well with conservative GC scanning")]
        public void RetainsStringUntilCollected()
        {
            // Add a string to the cache using a non-inlinable method to make sure it's not reachable from a GC root.
            AddString("Random string ", "test", (string cachedString) =>
            {
                _cache.GetDebugInfo().ShouldBe(new WeakStringCache.DebugInfo()
                {
                    LiveStringCount = 1,
                    CollectedStringCount = 0,
                });
            });

            // Trigger full GC.
            GC.Collect();

            // The handle is still in the cache but it's unused now as the string has been collected.
            _cache.GetDebugInfo().ShouldBe(new WeakStringCache.DebugInfo()
            {
                LiveStringCount = 0,
                CollectedStringCount = 1,
            });

            // Ask the cache to get rid of unused handles.
            _cache.Scavenge();

            // The cache should be empty now.
            _cache.GetDebugInfo().ShouldBe(new WeakStringCache.DebugInfo()
            {
                LiveStringCount = 0,
                CollectedStringCount = 0,
            });
        }

        /// <summary>
        /// Same as RetainsStringUntilCollected but with multiple strings sharing the same hash code.
        /// </summary>
        /// <remarks>
        /// Disabled on MacOS Mono because it doesn't play well with conservative GC scanning.
        /// https://www.mono-project.com/docs/advanced/garbage-collector/sgen/#precise-stack-marking
        /// </remarks>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void RetainsLastStringWithGivenHashCode()
        {
            // Add 3 strings with the same hash code.
            AddStringsWithSameHashCode(3);

            // Trigger full GC.
            GC.Collect();

            // The handle is still in the cache but it's unused now as the strings have been collected.
            _cache.GetDebugInfo().ShouldBe(new WeakStringCache.DebugInfo()
            {
                LiveStringCount = 0,
                CollectedStringCount = 1,
            });

            // Ask the cache to get rid of unused handles.
            _cache.Scavenge();

            // The cache should be empty now.
            _cache.GetDebugInfo().ShouldBe(new WeakStringCache.DebugInfo()
            {
                LiveStringCount = 0,
                CollectedStringCount = 0,
            });
        }
    }
}
