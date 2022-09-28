using System;
using System.Collections.Generic;

namespace BenchmarkDotNet.Validators
{
    public class ExecutionValidator : ExecutionValidatorBase
    {
        public static readonly ExecutionValidator DontFailOnError = new ExecutionValidator(false);
        public static readonly ExecutionValidator FailOnError = new ExecutionValidator(true);

        private ExecutionValidator(bool failOnError)
            : base(failOnError) { }

        private static readonly object Obj = new ();

        protected override void ExecuteBenchmarks(IEnumerable<BenchmarkExecutor> executors, List<ValidationError> errors)
        {
            lock (Obj)
            {
                foreach (var executor in executors)
                {
                    try
                    {
                        executor.Invoke();
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new ValidationError(
                            TreatsWarningsAsErrors,
                            $"Failed to execute benchmark '{executor.BenchmarkCase.DisplayInfo}', exception was: '{GetDisplayExceptionMessage(ex)}'",
                            executor.BenchmarkCase));
                    }
                }
            }
        }
    }
}