using System;
using System.Collections.Generic;
using BenchmarkDotNet.Loggers;

namespace BenchmarkDotNet.Running
{
    internal interface IUserInteraction
    {
        void PrintNoBenchmarksError(ILogger logger);

        void PrintWrongFilterInfo(IReadOnlyList<Type> allBenchmarkTypes, ILogger logger, string[] userFilters);

        IReadOnlyList<Type> AskUser(IReadOnlyList<Type> allBenchmarkTypes, ILogger logger);
    }
}