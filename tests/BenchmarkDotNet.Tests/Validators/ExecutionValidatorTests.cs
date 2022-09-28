﻿using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using Xunit;

namespace BenchmarkDotNet.Tests.Validators
{
    public class MyTests1
    {
        [Fact]
        public void Method1()
        {
            var task = new TestClass1().Foo();
            var isAsync = TryGetTaskResult(task, out var result);

            Assert.True(isAsync);
            Assert.Equal(1, result);
        }

        public class TestClass1
        {
            public async Task<int> Foo()
            {
                await Task.Delay(1);
                return 1;
            }
        }

        // https://stackoverflow.com/a/52500763
        public static bool TryGetTaskResult(Task task, out object result)
        {
            result = null;

            var voidTaskType = typeof(Task<>).MakeGenericType(Type.GetType("System.Threading.Tasks.VoidTaskResult"));
            if (voidTaskType.IsInstanceOfType(task))
            {
                task.GetAwaiter().GetResult();
                return false;
            }

            var property = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            if (property is null)
            {
                return false;
            }

            result = property.GetValue(task);
            return true;
        }
    }

    public class MyTests2
    {
        [Fact]
        public void Method2()
        {
            var task = new TestClass2().GlobalSetup();
            var isAsync = MyTests1.TryGetTaskResult(task, out var result);

            Assert.True(isAsync);
            Assert.Equal(42, result);
            Assert.True(TestClass2.WasCalled);
        }

        public class TestClass2
        {
            public static bool WasCalled;

            public async Task<int> GlobalSetup()
            {
                await Task.Delay(1);
                WasCalled = true;
                return 42;
            }
        }

        [Fact]
        public void Method3()
        {
            var task = new TestClass3().GlobalCleanup();
            var isAsync = MyTests1.TryGetTaskResult(task.AsTask(), out var result);

            Assert.True(isAsync);
            Assert.Null(result);
            Assert.True(TestClass3.WasCalled);
        }

        public class TestClass3
        {
            public static bool WasCalled;

            public async ValueTask GlobalCleanup()
            {
                await Task.Delay(1);
                WasCalled = true;
            }
        }
    }

    [Collection("Disable parallelism")]
    public class ExecutionValidatorTests
    {
        [Fact]
        public void AsyncTaskGlobalSetupIsExecuted()
        {
            var validationErrors = ExecutionValidator.FailOnError.Validate(BenchmarkConverter.TypeToBenchmarks(typeof(AsyncTaskGlobalSetup))).ToList();

            Assert.True(AsyncTaskGlobalSetup.WasCalled);
            Assert.Empty(validationErrors);
        }

        public class AsyncTaskGlobalSetup
        {
            public static bool WasCalled;

            [GlobalSetup]
            public async Task GlobalSetup()
            {
                await Task.Delay(1);

                WasCalled = true;
            }

            [Benchmark]
            public void NonThrowing() { }
        }

        [Fact]
        public void AsyncTaskGlobalCleanupIsExecuted()
        {
            var validationErrors = ExecutionValidator.FailOnError.Validate(BenchmarkConverter.TypeToBenchmarks(typeof(AsyncTaskGlobalCleanup))).ToList();

            Assert.True(AsyncTaskGlobalCleanup.WasCalled);
            Assert.Empty(validationErrors);
        }

        public class AsyncTaskGlobalCleanup
        {
            public static bool WasCalled;

            [GlobalCleanup]
            public async Task GlobalCleanup()
            {
                await Task.Delay(1);

                WasCalled = true;
            }

            [Benchmark]
            public void NonThrowing() { }
        }

        [Fact]
        public void AsyncGenericTaskGlobalSetupIsExecuted()
        {
            var validationErrors = ExecutionValidator.FailOnError.Validate(BenchmarkConverter.TypeToBenchmarks(typeof(AsyncGenericTaskGlobalSetup))).ToList();

            Assert.True(AsyncGenericTaskGlobalSetup.WasCalled);
            Assert.Empty(validationErrors);
        }

        public class AsyncGenericTaskGlobalSetup
        {
            public static bool WasCalled;

            [GlobalSetup]
            public async Task<int> GlobalSetup()
            {
                await Task.Delay(1);

                WasCalled = true;

                return 42;
            }

            [Benchmark]
            public void NonThrowing() { }
        }

        [Fact]
        public void AsyncGenericTaskGlobalCleanupIsExecuted()
        {
            var validationErrors = ExecutionValidator.FailOnError.Validate(BenchmarkConverter.TypeToBenchmarks(typeof(AsyncGenericTaskGlobalCleanup))).ToList();

            Assert.True(AsyncGenericTaskGlobalCleanup.WasCalled);
            Assert.Empty(validationErrors);
        }

        public class AsyncGenericTaskGlobalCleanup
        {
            public static bool WasCalled;

            [GlobalCleanup]
            public async Task<int> GlobalCleanup()
            {
                await Task.Delay(1);

                WasCalled = true;

                return 42;
            }

            [Benchmark]
            public void NonThrowing() { }
        }

        [Fact]
        public void AsyncValueTaskGlobalSetupIsExecuted()
        {
            var validationErrors = ExecutionValidator.FailOnError.Validate(BenchmarkConverter.TypeToBenchmarks(typeof(AsyncValueTaskGlobalSetup))).ToList();

            Assert.True(AsyncValueTaskGlobalSetup.WasCalled);
            Assert.Empty(validationErrors);
        }

        public class AsyncValueTaskGlobalSetup
        {
            public static bool WasCalled;

            [GlobalSetup]
            public async ValueTask GlobalSetup()
            {
                await Task.Delay(1);

                WasCalled = true;
            }

            [Benchmark]
            public void NonThrowing() { }
        }

        [Fact]
        public void AsyncValueTaskGlobalCleanupIsExecuted()
        {
            var validationErrors = ExecutionValidator.FailOnError.Validate(BenchmarkConverter.TypeToBenchmarks(typeof(AsyncValueTaskGlobalCleanup))).ToList();

            Assert.True(AsyncValueTaskGlobalCleanup.WasCalled);
            Assert.Empty(validationErrors);
        }

        public class AsyncValueTaskGlobalCleanup
        {
            public static bool WasCalled;

            [GlobalCleanup]
            public async ValueTask GlobalCleanup()
            {
                await Task.Delay(1);

                WasCalled = true;
            }

            [Benchmark]
            public void NonThrowing() { }
        }

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
            public async Task<int> GlobalSetup()
            {
                await Task.Delay(1);

                WasCalled = true;

                return 42;
            }

            [Benchmark]
            public void NonThrowing() { }
        }

        [Fact]
        public void Wtf1()
        {
            var validationErrors = ExecutionValidator.FailOnError.Validate(BenchmarkConverter.TypeToBenchmarks(typeof(Wtf1Class))).ToList();

            Assert.True(Wtf1Class.WasCalled);
            Assert.Empty(validationErrors);
        }

        public class Wtf1Class
        {
            public static bool WasCalled;

            [GlobalCleanup]
            public async Task<int> GlobalCleanup() //ValueTask<int>
            {
                await Task.Delay(1);

                WasCalled = true;

                return 42;
            }

            [Benchmark]
            public void NonThrowing() { }
        }

        [Fact]
        public void AsyncIterationSetupIsNotAllowed()
        {
            var validationErrors = ExecutionValidator.FailOnError.Validate(BenchmarkConverter.TypeToBenchmarks(typeof(AsyncIterationSetupIsNotAllowedClass))).ToList();

            Assert.NotEmpty(validationErrors);
            Assert.StartsWith("[IterationSetup] cannot be async. Error in type ", validationErrors.Single().Message);
        }

        public class AsyncIterationSetupIsNotAllowedClass
        {
            [IterationSetup]
            public Task Setup() => Task.CompletedTask;

            [Benchmark]
            public void Foo() { }
        }

        [Fact]
        public void AsyncIterationCleanupIsNotAllowed()
        {
            var validationErrors = ExecutionValidator.FailOnError.Validate(BenchmarkConverter.TypeToBenchmarks(typeof(AsyncIterationCleanupIsNotAllowedClass))).ToList();

            Assert.NotEmpty(validationErrors);
            Assert.StartsWith("[IterationCleanup] cannot be async. Error in type ", validationErrors.Single().Message);
        }

        public class AsyncIterationCleanupIsNotAllowedClass
        {
            [IterationCleanup]
            public Task Cleanup() => Task.CompletedTask;

            [Benchmark]
            public void Foo() { }
        }

        [Fact]
        public void SetupsWithCleanupsAreCalledInCorrectOrder()
        {
            var validationErrors = ExecutionValidator.FailOnError.Validate(BenchmarkConverter.TypeToBenchmarks(typeof(SetupsAndCleanups))).ToList();

            Assert.True(SetupsAndCleanups.GlobalSetupIsCalled);
            Assert.True(SetupsAndCleanups.IterationSetupIsCalled);
            Assert.True(SetupsAndCleanups.BenchmarkIsCalled);
            Assert.True(SetupsAndCleanups.IterationCleanupIsCalled);
            Assert.True(SetupsAndCleanups.GlobalCleanupIsCalled);

            Assert.Empty(validationErrors);
        }

        public class SetupsAndCleanups
        {
            public static bool GlobalSetupIsCalled;
            public static bool IterationSetupIsCalled;
            public static bool BenchmarkIsCalled;
            public static bool IterationCleanupIsCalled;
            public static bool GlobalCleanupIsCalled;

            [GlobalSetup]
            public void GlobalSetup() =>
                GlobalSetupIsCalled = true;

            [IterationSetup]
            public void IterationSetup()
            {
                if (!GlobalSetupIsCalled)
                    throw new Exception("[GlobalSetup] is not called");

                IterationSetupIsCalled = true;
            }

            [Benchmark]
            public void Benchmark()
            {
                if (!IterationSetupIsCalled)
                    throw new Exception("[IterationSetup] is not called");

                BenchmarkIsCalled = true;
            }

            [IterationCleanup]
            public void IterationCleanup()
            {
                if (!BenchmarkIsCalled)
                    throw new Exception("[Benchmark] is not called");

                IterationCleanupIsCalled = true;
            }

            [GlobalCleanup]
            public void GlobalCleanup()
            {
                if (!IterationCleanupIsCalled)
                    throw new Exception("[IterationCleanup] is not called");

                GlobalCleanupIsCalled = true;
            }
        }

        [Fact]
        public void AsyncSetupsWithCleanupsAreCalledInCorrectOrder()
        {
            var validationErrors = ExecutionValidator.FailOnError.Validate(BenchmarkConverter.TypeToBenchmarks(typeof(AsyncSetupsAndCleanups))).ToList();

            Assert.True(AsyncSetupsAndCleanups.AsyncGlobalSetupIsCalled);
            Assert.True(AsyncSetupsAndCleanups.IterationSetupIsCalled);
            Assert.True(AsyncSetupsAndCleanups.AsyncBenchmarkIsCalled);
            Assert.True(AsyncSetupsAndCleanups.IterationCleanupIsCalled);
            Assert.True(AsyncSetupsAndCleanups.AsyncGlobalCleanupIsCalled);

            Assert.Empty(validationErrors);
        }

        public class AsyncSetupsAndCleanups
        {
            public static bool AsyncGlobalSetupIsCalled;
            public static bool IterationSetupIsCalled;
            public static bool AsyncBenchmarkIsCalled;
            public static bool IterationCleanupIsCalled;
            public static bool AsyncGlobalCleanupIsCalled;

            [GlobalSetup]
            public async Task GlobalSetup()
            {
                await Task.Delay(1);
                AsyncGlobalSetupIsCalled = true;
            }

            [IterationSetup]
            public void IterationSetup()
            {
                if (!AsyncGlobalSetupIsCalled)
                    throw new Exception("[GlobalSetup] is not called");

                IterationSetupIsCalled = true;
            }

            [Benchmark]
            public async Task Benchmark()
            {
                if (!IterationSetupIsCalled)
                    throw new Exception("[IterationSetup] is not called");

                await Task.Delay(1);
                AsyncBenchmarkIsCalled = true;
            }

            [IterationCleanup]
            public void IterationCleanup()
            {
                if (!AsyncBenchmarkIsCalled)
                    throw new Exception("[Benchmark] is not called");

                IterationCleanupIsCalled = true;
            }

            [GlobalCleanup]
            public async Task GlobalCleanup()
            {
                if (!IterationCleanupIsCalled)
                    throw new Exception("[IterationCleanup] is not called");

                await Task.Delay(1);
                AsyncGlobalCleanupIsCalled = true;
            }
        }
    }
}