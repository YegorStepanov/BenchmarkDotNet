using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using Xunit;

namespace BenchmarkDotNet.Tests.Validators
{
    public class ReturnValueValidatorTests
    {
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