using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Extensions;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Parameters;

namespace BenchmarkDotNet.Running
{
    public static partial class BenchmarkConverter
    {
        private const BindingFlags AllMethodsFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static BenchmarkRunInfo TypeToBenchmarks(Type type, IConfig config = null)
        {
            if (type.IsGenericTypeDefinition) //todo: remove it
                throw new InvalidBenchmarkDeclarationException($"{type.Name} is generic type definition, use BenchmarkSwitcher for it"); // for "open generic types" should be used BenchmarkSwitcher

            // We should check all methods including private to notify users about private methods with the [Benchmark] attribute
            var benchmarkMethods = GetOrderedBenchmarkMethods(type.GetMethods(AllMethodsFlags));

            return MethodsToBenchmarksWithFullConfig(type, benchmarkMethods, config);
        }

        public static BenchmarkRunInfo MethodsToBenchmarks(Type containingType, MethodInfo[] benchmarkMethods, IConfig config = null)
            => MethodsToBenchmarksWithFullConfig(containingType, GetOrderedBenchmarkMethods(benchmarkMethods), config);

        private static MethodInfo[] GetOrderedBenchmarkMethods(MethodInfo[] methods)
            => methods
                .Select(method => (method, attribute: method.ResolveAttribute<BenchmarkAttribute>()))
                .Where(pair => pair.attribute is not null)
                .OrderBy(pair => pair.attribute.SourceCodeFile)
                .ThenBy(pair => pair.attribute.SourceCodeLineNumber)
                .Select(pair => pair.method)
                .ToArray();

        private static BenchmarkRunInfo MethodsToBenchmarksWithFullConfig(Type type, MethodInfo[] benchmarkMethods, IConfig config)
        {
            var configPerType = GetFullTypeConfig(type, config);
            var paramsInstances = ParamBuilder.CreateForParams(type, configPerType.SummaryStyle);

            var benchmarks = new List<BenchmarkCase>();
            foreach (var target in GetTargets(benchmarkMethods, type))
            {
                var argumentsInstances = ParamBuilder.CreateForArguments(target.WorkloadMethod, target.Type, configPerType.SummaryStyle).ToArray();

                var parameterInstances =
                    (from paramInstances in paramsInstances
                     from argumentInstances in argumentsInstances
                     select new ParameterInstances(paramInstances.Items.Concat(argumentInstances.Items).ToArray())).ToArray();

                var configPerMethod = GetFullMethodConfig(target.WorkloadMethod, configPerType);

                var benchmarksForTarget =
                    from job in configPerMethod.GetJobs()
                    from parameterInstance in parameterInstances
                    select BenchmarkCase.Create(target, job, parameterInstance, configPerMethod);

                benchmarks.AddRange(GetFilteredBenchmarks(benchmarksForTarget, configPerMethod.GetFilters()));
            }

            var orderedBenchmarks = configPerType.Orderer.GetExecutionOrder(benchmarks.ToImmutableArray()).ToArray();

            return new BenchmarkRunInfo(orderedBenchmarks, type, configPerType);
        }

        private static ImmutableConfig GetFullTypeConfig(Type type, IConfig config)
        {
            config ??= DefaultConfig.Instance;

            var typeAttributes = type.GetCustomAttributes(true).OfType<IConfigSource>();
            var assemblyAttributes = type.Assembly.GetCustomAttributes().OfType<IConfigSource>();

            foreach (var configFromAttribute in assemblyAttributes.Concat(typeAttributes))
                config = ManualConfig.Union(config, configFromAttribute.Config);

            return ImmutableConfigBuilder.Create(config);
        }

        private static ImmutableConfig GetFullMethodConfig(MethodInfo method, ImmutableConfig typeConfig)
        {
            var methodAttributes = method.GetCustomAttributes(true).OfType<IConfigSource>();

            if (!methodAttributes.Any()) // the most common case
                return typeConfig;

            var config = ManualConfig.Create(typeConfig);
            foreach (var configFromAttribute in methodAttributes)
                config = ManualConfig.Union(config, configFromAttribute.Config);

            return ImmutableConfigBuilder.Create(config);
        }

        private static IEnumerable<Descriptor> GetTargets(MethodInfo[] targetMethods, Type type)
        {
            var allMethods = type.GetMethods(AllMethodsFlags); // benchmarkMethods can be filtered, without Setups, look #564

            var globalSetupMethods = GetAttributedMethods<GlobalSetupAttribute>(allMethods);
            var globalCleanupMethods = GetAttributedMethods<GlobalCleanupAttribute>(allMethods);
            var iterationSetupMethods = GetAttributedMethods<IterationSetupAttribute>(allMethods);
            var iterationCleanupMethods = GetAttributedMethods<IterationCleanupAttribute>(allMethods);

            return targetMethods.Select(methodInfo =>
                CreateDescriptor(type,
                    GetTargetedMatchingMethod(methodInfo, globalSetupMethods),
                    methodInfo,
                    GetTargetedMatchingMethod(methodInfo, globalCleanupMethods),
                    GetTargetedMatchingMethod(methodInfo, iterationSetupMethods),
                    GetTargetedMatchingMethod(methodInfo, iterationCleanupMethods),
                    methodInfo.ResolveAttribute<BenchmarkAttribute>(),
                    targetMethods));
        }

        private static MethodInfo GetTargetedMatchingMethod(MethodInfo benchmarkMethod, Tuple<MethodInfo, TargetedAttribute>[] methods)
            => methods.Where(method => method.Item2.Match(benchmarkMethod)).Select(method => method.Item1).FirstOrDefault();

        private static Tuple<MethodInfo, TargetedAttribute>[] GetAttributedMethods<T>(MethodInfo[] methods) where T : TargetedAttribute
            => methods
                .SelectMany(m => m.GetCustomAttributes<T>().Select(attr => new Tuple<MethodInfo, TargetedAttribute>(m, attr)))
                .OrderByDescending(x => x.Item2.Targets?.Length ?? 0).ToArray();

        // return methods.SelectMany(m => m.GetCustomAttributes<T>()
        //     .Select(attr =>
        //     {
        //         AssertMethodIsAccessible(methodName, m);
        //         AssertMethodHasCorrectSignature(methodName, m);
        //         AssertMethodIsNotGeneric(methodName, m);
        //
        //         return new Tuple<MethodInfo, TargetedAttribute>(m, attr);
        //     })).OrderByDescending(x => x.Item2.Targets?.Length ?? 0).ToArray();

        private static Descriptor CreateDescriptor(
            Type type,
            MethodInfo globalSetupMethod,
            MethodInfo methodInfo,
            MethodInfo globalCleanupMethod,
            MethodInfo iterationSetupMethod,
            MethodInfo iterationCleanupMethod,
            BenchmarkAttribute attr,
            MethodInfo[] targetMethods)
        {
            var target = new Descriptor(
                type,
                methodInfo,
                globalSetupMethod,
                globalCleanupMethod,
                iterationSetupMethod,
                iterationCleanupMethod,
                attr.Description,
                baseline: attr.Baseline,
                categories: GetCategories(methodInfo),
                operationsPerInvoke: attr.OperationsPerInvoke,
                methodIndex: Array.IndexOf(targetMethods, methodInfo));
            // AssertMethodHasCorrectSignature("Benchmark", methodInfo);
            // AssertMethodIsAccessible("Benchmark", methodInfo);
            // AssertMethodIsNotGeneric("Benchmark", methodInfo);
            return target;
        }

        private static string[] GetCategories(MethodInfo method)
        {
            var attributes = new List<BenchmarkCategoryAttribute>();
            attributes.AddRange(method.GetCustomAttributes(typeof(BenchmarkCategoryAttribute), false).OfType<BenchmarkCategoryAttribute>());
            var type = method.DeclaringType;
            if (type != null)
            {
                attributes.AddRange(type.GetTypeInfo().GetCustomAttributes(typeof(BenchmarkCategoryAttribute), false).OfType<BenchmarkCategoryAttribute>());
                attributes.AddRange(type.GetTypeInfo().Assembly.GetCustomAttributes().OfType<BenchmarkCategoryAttribute>());
            }
            if (attributes.Count == 0)
                return Array.Empty<string>();
            return attributes.SelectMany(attr => attr.Categories).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static ImmutableArray<BenchmarkCase> GetFilteredBenchmarks(IEnumerable<BenchmarkCase> benchmarks, IEnumerable<IFilter> filters)
            => benchmarks.Where(benchmark => filters.All(filter => filter.Predicate(benchmark))).ToImmutableArray();

        // private static void AssertMethodHasCorrectSignature(string methodType, MethodInfo methodInfo)
        // {
        //     if (methodInfo.GetParameters().Any() && !methodInfo.HasAttribute<ArgumentsAttribute>() && !methodInfo.HasAttribute<ArgumentsSourceAttribute>())
        //         throw new InvalidBenchmarkDeclarationException($"{methodType} method {methodInfo.Name} has incorrect signature.\nMethod shouldn't have any arguments.");
        // }
        //
        // private static void AssertMethodIsAccessible(string methodType, MethodInfo methodInfo)
        // {
        //     if (!methodInfo.IsPublic)
        //         throw new InvalidBenchmarkDeclarationException($"{methodType} method {methodInfo.Name} has incorrect access modifiers.\nMethod must be public.");
        //
        //     var declaringType = methodInfo.DeclaringType;
        //
        //     while (declaringType != null)
        //     {
        //         if (!declaringType.GetTypeInfo().IsPublic && !declaringType.GetTypeInfo().IsNestedPublic)
        //             throw new InvalidBenchmarkDeclarationException($"{declaringType.FullName} containing {methodType} method {methodInfo.Name} has incorrect access modifiers.\nDeclaring type must be public.");
        //
        //         declaringType = declaringType.DeclaringType;
        //     }
        // }
        //
        // private static void AssertMethodIsNotGeneric(string methodType, MethodInfo methodInfo)
        // {
        //     if (methodInfo.IsGenericMethod)
        //         throw new InvalidBenchmarkDeclarationException($"{methodType} method {methodInfo.Name} is generic.\nGeneric {methodType} methods are not supported.");
        // }
    }
}