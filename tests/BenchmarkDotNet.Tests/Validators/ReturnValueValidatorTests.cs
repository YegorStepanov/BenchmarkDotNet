using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BenchmarkDotNet.Extensions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using Xunit;

namespace BenchmarkDotNet.Tests.Validators
{
    public class ReturnValueValidatorTests
    {
        [Fact]
        public void AsyncGenericValueTaskGlobalSetupIsExecuted() //!
        {
            var validationErrors = ExecutionValidator.FailOnError.Validate(BenchmarkConverter.TypeToBenchmarks(typeof(AsyncGenericValueTaskGlobalSetup))).ToList();

            Assert.True(AsyncGenericValueTaskGlobalSetup.WasCalled);
            Assert.Empty(validationErrors);
        }

        public class AsyncGenericValueTaskGlobalSetup
        {
            public static bool WasCalled;

            [GlobalSetup]
            public async ValueTask<int> GlobalSetup()
            {
                await Task.Delay(1);

                WasCalled = true;

                return 42;
            }

            [Benchmark]
            public void NonThrowing() { }
        }

        [Fact]
        public void AsyncSetupIsSupported() //!
            => AssertConsistent<AsyncSetupIsSupportedClass>();

        public class AsyncSetupIsSupportedClass
        {
            [Benchmark]
            public async Task<int> Foo()
            {
                await Task.Delay(1);
                return 1;
            }

            [Benchmark]
            public int Bar() => 1;
        }

        private static void AssertConsistent<TBenchmark>()
        {
            var validationErrors = ReturnValueValidator.FailOnError.Validate(BenchmarkConverter.TypeToBenchmarks(typeof(TBenchmark))).ToList();

            Assert.Empty(validationErrors);
        }
    }
}