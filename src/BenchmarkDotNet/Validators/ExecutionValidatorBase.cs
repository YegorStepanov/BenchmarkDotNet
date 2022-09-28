using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Extensions;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace BenchmarkDotNet.Validators
{
    public abstract class ExecutionValidatorBase : IValidator
    {
        protected ExecutionValidatorBase(bool failOnError)
        {
            TreatsWarningsAsErrors = failOnError;
        }

        public bool TreatsWarningsAsErrors { get; }

        public IEnumerable<ValidationError> Validate(ValidationParameters validationParameters)
        {
            var errors = new List<ValidationError>();

            foreach (var typeGroup in validationParameters.Benchmarks.GroupBy(benchmark => benchmark.Descriptor.Type))
            {
                var executors = new List<BenchmarkExecutor>();

                foreach (var benchmark in typeGroup)
                {
                    if (!TryCreateBenchmarkTypeInstance(typeGroup.Key, errors, out var benchmarkTypeInstance))
                        continue;

                    if (!TryToFillParameters(benchmark, benchmarkTypeInstance, errors))
                        continue;

                    if (!TryToCallGlobalSetup(benchmarkTypeInstance, errors))
                        continue;

                    if (!TryToCallIterationSetup(benchmarkTypeInstance, errors))
                        continue;

                    executors.Add(new BenchmarkExecutor(benchmarkTypeInstance, benchmark));
                }

                ExecuteBenchmarks(executors, errors);

                foreach (var executor in executors)
                {
                    if (!TryToCallIterationCleanup(executor.Instance, errors))
                        continue;

                    TryToCallGlobalCleanup(executor.Instance, errors);
                }
            }

            return errors;
        }

        private bool TryCreateBenchmarkTypeInstance(Type type, List<ValidationError> errors, out object instance)
        {
            try
            {
                instance = Activator.CreateInstance(type);

                return true;
            }
            catch (Exception ex)
            {
                errors.Add(new ValidationError(
                    TreatsWarningsAsErrors,
                    $"Unable to create instance of {type.Name}, exception was: {GetDisplayExceptionMessage(ex)}"));

                instance = null;
                return false;
            }
        }

        private bool TryToCallGlobalSetup(object benchmarkTypeInstance, List<ValidationError> errors)
        {
            return TryToCallGlobalMethod<GlobalSetupAttribute>(benchmarkTypeInstance, errors, true);
        }

        private void TryToCallGlobalCleanup(object benchmarkTypeInstance, List<ValidationError> errors)
        {
            TryToCallGlobalMethod<GlobalCleanupAttribute>(benchmarkTypeInstance, errors, true);
        }

        private bool TryToCallIterationSetup(object benchmarkTypeInstance, List<ValidationError> errors)
        {
            return TryToCallGlobalMethod<IterationSetupAttribute>(benchmarkTypeInstance, errors, false);
        }

        private bool TryToCallIterationCleanup(object benchmarkTypeInstance, List<ValidationError> errors)
        {
            return TryToCallGlobalMethod<IterationCleanupAttribute>(benchmarkTypeInstance, errors, false);
        }

        private bool TryToCallGlobalMethod<T>(object benchmarkTypeInstance, List<ValidationError> errors, bool canBeAsync) where T : Attribute
        {
            var methods = benchmarkTypeInstance
                .GetType()
                .GetAllMethods()
                .Where(methodInfo => methodInfo.GetCustomAttributes(false).OfType<T>().Any())
                .ToArray();

            if (!methods.Any())
            {
                return true;
            }

            if (methods.Count(methodInfo => !methodInfo.IsVirtual) > 1)
            {
                errors.Add(new ValidationError(
                    TreatsWarningsAsErrors,
                    $"Only single [{GetAttributeName(typeof(T))}] method is allowed per type, type {benchmarkTypeInstance.GetType().Name} has few"));

                return false;
            }

            try
            {
                var result = methods.First().Invoke(benchmarkTypeInstance, null);

                var isAsyncMethod = TryAwaitTask(result, out _);

                if (!canBeAsync && isAsyncMethod)
                {
                    errors.Add(new ValidationError(
                        TreatsWarningsAsErrors,
                        $"[{GetAttributeName(typeof(T))}] cannot be async. Error in type {benchmarkTypeInstance.GetType().Name}"));

                    return false;
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ValidationError(
                    TreatsWarningsAsErrors,
                    $"Failed to execute [{GetAttributeName(typeof(T))}] for {benchmarkTypeInstance.GetType().Name}, exception was {GetDisplayExceptionMessage(ex)}"));

                return false;
            }

            return true;
        }

        private static bool TryAwaitTask(object task, out object result)
        {
            result = null;

            if (task is null)
            {
                return false;
            }

            var getResultMethod = AwaitHelper.GetGetResultMethod(task.GetType());

            if (getResultMethod is null)
            {
                return false;
            }

            result = getResultMethod.Invoke(null, new[] { task });

            return getResultMethod.ReturnType != typeof(void) || getResultMethod.ReturnType.Name != "VoidTaskResult";
        }

// https://stackoverflow.com/a/52500763
        private static bool TryGetTaskResult(Task task, out object result)
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

        private bool TryToFillParameters(BenchmarkCase benchmark, object benchmarkTypeInstance, List<ValidationError> errors)
        {
            if (ValidateMembers<ParamsAttribute>(benchmarkTypeInstance, errors))
                return false;

            if (ValidateMembers<ParamsSourceAttribute>(benchmarkTypeInstance, errors))
                return false;

            bool hasError = false;

            foreach (var parameter in benchmark.Parameters.Items)
            {
                // InProcessNoEmitToolchain does not support arguments
                if (!parameter.IsArgument)
                {
                    try
                    {
                        InProcessNoEmitRunner.FillMember(benchmarkTypeInstance, benchmark, parameter);
                    }
                    catch (Exception ex)
                    {
                        hasError = true;
                        errors.Add(new ValidationError(
                            TreatsWarningsAsErrors,
                            ex.Message,
                            benchmark));
                    }
                }
            }

            return !hasError;
        }

        private bool ValidateMembers<T>(object benchmarkTypeInstance, List<ValidationError> errors) where T : Attribute
        {
            const BindingFlags reflectionFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            bool hasError = false;

            foreach (var member in benchmarkTypeInstance.GetType().GetTypeMembersWithGivenAttribute<T>(reflectionFlags))
            {
                if (!member.IsPublic)
                {
                    errors.Add(new ValidationError(
                        TreatsWarningsAsErrors,
                        $"Member \"{member.Name}\" must be public if it has the [{GetAttributeName(typeof(T))}] attribute applied to it, {member.Name} of {benchmarkTypeInstance.GetType().Name} has not"));

                    hasError = true;
                }
            }

            return hasError;
        }

        private static string GetAttributeName(Type type) => type.Name.Replace("Attribute", string.Empty);

        protected static string GetDisplayExceptionMessage(Exception ex)
        {
            if (ex is TargetInvocationException targetInvocationException)
                ex = targetInvocationException.InnerException;

            return ex?.Message ?? "Unknown error";
        }

        protected abstract void ExecuteBenchmarks(IEnumerable<BenchmarkExecutor> executors, List<ValidationError> errors);

        protected class BenchmarkExecutor
        {
            public object Instance { get; }
            public BenchmarkCase BenchmarkCase { get; }

            public BenchmarkExecutor(object instance, BenchmarkCase benchmarkCase)
            {
                Instance = instance;
                BenchmarkCase = benchmarkCase;
            }

            private static readonly object Obj = new ();

            public object Invoke()
            {
                var arguments = BenchmarkCase.Parameters.Items
                    .Where(parameter => parameter.IsArgument)
                    .Select(argument => argument.Value)
                    .ToArray();

                lock (Obj)
                {
                    var result = BenchmarkCase.Descriptor.WorkloadMethod.Invoke(Instance, arguments);

                    if (TryAwaitTask(result, out var taskResult))
                        result = taskResult;

                    return result;
                }
            }
        }
    }

    public static class AwaitHelper
    {
        private class ValueTaskWaiter
        {
            // We use thread static field so that multiple threads can use individual lock object and callback.
            [ThreadStatic]
            private static ValueTaskWaiter ts_current;
            internal static ValueTaskWaiter Current => ts_current ??= new ValueTaskWaiter();

            private readonly Action awaiterCallback;
            private bool awaiterCompleted;

            private ValueTaskWaiter()
            {
                awaiterCallback = AwaiterCallback;
            }

            private void AwaiterCallback()
            {
                lock (this)
                {
                    awaiterCompleted = true;
                    System.Threading.Monitor.Pulse(this);
                }
            }

            // Hook up a callback instead of converting to Task to prevent extra allocations on each benchmark run.
            internal void Wait(ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter awaiter)
            {
                lock (this)
                {
                    awaiterCompleted = false;
                    awaiter.UnsafeOnCompleted(awaiterCallback);
                    // Check if the callback executed synchronously before blocking.
                    if (!awaiterCompleted)
                    {
                        System.Threading.Monitor.Wait(this);
                    }
                }
            }

            internal void Wait<T>(ConfiguredValueTaskAwaitable<T>.ConfiguredValueTaskAwaiter awaiter)
            {
                lock (this)
                {
                    awaiterCompleted = false;
                    awaiter.UnsafeOnCompleted(awaiterCallback);
                    // Check if the callback executed synchronously before blocking.
                    if (!awaiterCompleted)
                    {
                        System.Threading.Monitor.Wait(this);
                    }
                }
            }
        }

        // we use GetAwaiter().GetResult() because it's fastest way to obtain the result in blocking way,
        // and will eventually throw actual exception, not aggregated one
        public static void GetResult(Task task) => task.GetAwaiter().GetResult();

        public static T GetResult<T>(Task<T> task) => task.GetAwaiter().GetResult();

        // ValueTask can be backed by an IValueTaskSource that only supports asynchronous awaits, so we have to hook up a callback instead of calling .GetAwaiter().GetResult() like we do for Task.
        // The alternative is to convert it to Task using .AsTask(), but that causes allocations which we must avoid for memory diagnoser.
        public static void GetResult(ValueTask task)
        {
            // Don't continue on the captured context, as that may result in a deadlock if the user runs this in-process.
            var awaiter = task.ConfigureAwait(false).GetAwaiter();
            if (!awaiter.IsCompleted)
            {
                ValueTaskWaiter.Current.Wait(awaiter);
            }
            awaiter.GetResult();
        }

        public static T GetResult<T>(ValueTask<T> task)
        {
            // Don't continue on the captured context, as that may result in a deadlock if the user runs this in-process.
            var awaiter = task.ConfigureAwait(false).GetAwaiter();
            if (!awaiter.IsCompleted)
            {
                ValueTaskWaiter.Current.Wait(awaiter);
            }
            return awaiter.GetResult();
        }

        internal static MethodInfo GetGetResultMethod(Type taskType)
        {
            if (!taskType.IsGenericType)
            {
                return typeof(AwaitHelper).GetMethod(nameof(AwaitHelper.GetResult), BindingFlags.Public | BindingFlags.Static, null, new Type[1] { taskType }, null);
            }

            Type compareType = taskType.GetGenericTypeDefinition() == typeof(ValueTask<>) ? typeof(ValueTask<>)
                : typeof(Task).IsAssignableFrom(taskType.GetGenericTypeDefinition()) ? typeof(Task<>)
                : null;
            if (compareType == null)
            {
                return null;
            }
            var resultType = taskType
                .GetMethod(nameof(Task.GetAwaiter), BindingFlags.Public | BindingFlags.Instance)
                .ReturnType
                .GetMethod(nameof(TaskAwaiter.GetResult), BindingFlags.Public | BindingFlags.Instance)
                .ReturnType;
            return typeof(AwaitHelper).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m =>
                {
                    if (m.Name != nameof(AwaitHelper.GetResult)) return false;
                    Type paramType = m.GetParameters().First().ParameterType;
                    // We have to compare the types indirectly, == check doesn't work.
                    return paramType.Assembly == compareType.Assembly && paramType.Namespace == compareType.Namespace && paramType.Name == compareType.Name;
                })
                .MakeGenericMethod(new[] { resultType });
        }
    }
}