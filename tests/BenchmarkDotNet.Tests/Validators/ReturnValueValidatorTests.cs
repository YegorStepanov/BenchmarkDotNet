using System;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Portability;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using Xunit;
using Xunit.Abstractions;

namespace BenchmarkDotNet.Tests.Validators
{
    [Collection("Disable parallelism")]
    public class ReturnValueValidatorTests
    {
        private readonly ITestOutputHelper testOutputHelper;

        public ReturnValueValidatorTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void Wtf2() //!
        {
            testOutputHelper.WriteLine("YEGOR: RETURN Wtf2" + $" {RuntimeInformation.GetRuntimeVersion()}");
            AssertConsistent<Wtf2Class>();
        }

        public class Wtf2Class
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