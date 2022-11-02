using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Code;
using BenchmarkDotNet.Extensions;
using BenchmarkDotNet.Reports;

namespace BenchmarkDotNet.Parameters
{
    internal static class ParamBuilder
    {
        public static IReadOnlyList<ParameterInstances> CreateForParams(Type type, SummaryStyle summaryStyle)
        {
            IEnumerable<ParameterDefinition> GetDefinitions<TAttribute>(Func<TAttribute, Type, object[]> getValidValues) where TAttribute : PriorityAttribute
            {
                const BindingFlags reflectionFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                var allMembers = type.GetTypeMembersWithGivenAttribute<TAttribute>(reflectionFlags);
                return allMembers.Select(member =>
                    new ParameterDefinition(
                        member.Name,
                        member.IsStatic,
                        getValidValues(member.Attribute, member.ParameterType),
                        false,
                        member.ParameterType,
                        member.Attribute.Priority));
            }

            var paramsDefinitions = GetDefinitions<ParamsAttribute>((attribute, parameterType) => GetValidValues(attribute.Values, parameterType));

            var paramsSourceDefinitions = GetDefinitions<ParamsSourceAttribute>((attribute, parameterType) =>
            {
                var paramsValues = GetValidValuesForParamsSource(type, attribute.Name);

                if (paramsValues.source == null)
                    return paramsValues.values;

                return SmartParamBuilder.CreateForParams(parameterType, paramsValues.source, paramsValues.values);
            });

            var paramsAllValuesDefinitions = GetDefinitions<ParamsAllValuesAttribute>((_, parameterType) => GetAllValidValues(parameterType));

            var definitions = paramsDefinitions.Concat(paramsSourceDefinitions).Concat(paramsAllValuesDefinitions).ToArray();
            return new ParameterDefinitions(definitions).Expand(summaryStyle);
        }

        public static IEnumerable<ParameterInstances> CreateForArguments(MethodInfo benchmark, Type target, SummaryStyle summaryStyle)
        {
            var argumentsAttributes = benchmark.GetCustomAttributes<PriorityAttribute>();
            int priority = argumentsAttributes.Select(attribute => attribute.Priority).Sum();

            var parameterDefinitions = benchmark.GetParameters()
                .Select(parameter => new ParameterDefinition(parameter.Name, false, Array.Empty<object>(), true, parameter.ParameterType, priority))
                .ToArray();

            if (parameterDefinitions.IsEmpty())
            {
                yield return new ParameterInstances(Array.Empty<ParameterInstance>());
                yield break;
            }

            foreach (var argumentsAttribute in benchmark.GetCustomAttributes<ArgumentsAttribute>())
            {
                // if (parameterDefinitions.Length != argumentsAttribute.Values.Length)
                //     throw new InvalidOperationException($"Benchmark {benchmark.Name} has invalid number of defined arguments provided with [Arguments]! {argumentsAttribute.Values.Length} instead of {parameterDefinitions.Length}.");

                yield return new ParameterInstances(
                    argumentsAttribute
                        .Values
                        .Select((value, index) =>
                        {
                            var definition = parameterDefinitions[index];
                            var type = definition.ParameterType;
                            return new ParameterInstance(definition, Map(value, type), summaryStyle);
                        })
                        .ToArray());
            }

            if (!benchmark.HasAttribute<ArgumentsSourceAttribute>())
                yield break;

            var argumentsSourceAttribute = benchmark.GetCustomAttribute<ArgumentsSourceAttribute>();

            var valuesInfo = GetValidValuesForParamsSource(target, argumentsSourceAttribute.Name);

            if (valuesInfo.source == null)
                yield break;

            for (int sourceIndex = 0; sourceIndex < valuesInfo.values.Length; sourceIndex++)
                yield return SmartParamBuilder.CreateForArguments(benchmark, parameterDefinitions, valuesInfo, sourceIndex, summaryStyle);
        }

        private static object[] GetValidValues(object[] values, Type parameterType)
        {
            if (values == null && parameterType.IsNullable())
            {
                return new object[] { null };
            }

            return values?.Select(value => Map(value, parameterType)).ToArray();
        }

        private static object Map(object providedValue, Type type)
        {
            if (providedValue == null)
                return null;

            if (providedValue.GetType().IsArray)
            {
                return ArrayParam<IParam>.FromObject(providedValue);
            }
            // Usually providedValue contains all needed type information,
            // but in case of F# enum types in attributes are erased.
            // We can to restore them from types of arguments and fields.
            // See also:
            // https://github.com/dotnet/fsharp/issues/995
            if (providedValue.GetType().IsEnum || type.IsEnum)
            {
                return EnumParam.FromObject(providedValue, type);
            }
            return providedValue;
        }

        private static (MemberInfo source, object[] values) GetValidValuesForParamsSource(Type parentType, string sourceName)
        {
            var paramsSourceMethod = parentType.GetAllMethods().SingleOrDefault(method => method.Name == sourceName); //&& method.IsPublic

            if (paramsSourceMethod != default)
                return (paramsSourceMethod, ToArray(
                    paramsSourceMethod.Invoke(paramsSourceMethod.IsStatic ? null : Activator.CreateInstance(parentType), null)));

            var paramsSourceProperty = parentType.GetAllProperties().SingleOrDefault(property => property.Name == sourceName); //&& property.GetMethod.IsPublic

            if (paramsSourceProperty != default)
                return (paramsSourceProperty, ToArray(
                    paramsSourceProperty.GetValue(paramsSourceProperty.GetMethod.IsStatic ? null : Activator.CreateInstance(parentType))));

            return (null, Array.Empty<object>());
        }

        private static object[] ToArray(object sourceValue)
        {
            // if (!(sourceValue is IEnumerable collection))
            //     throw new InvalidBenchmarkDeclarationException($"{memberInfo.Name} of type {type.Name} does not implement IEnumerable, unable to read values for [ParamsSource]");

            if (sourceValue is IEnumerable collection)
                return collection.Cast<object>().ToArray();

            return new object[] { sourceValue };
        }

        private static object[] GetAllValidValues(Type parameterType)
        {
            if (parameterType == typeof(bool))
                return new object[] { false, true };

            if (parameterType.GetTypeInfo().IsEnum)
            {
                if (parameterType.GetTypeInfo().IsDefined(typeof(FlagsAttribute)))
                    return new object[] { Activator.CreateInstance(parameterType) };

                return Enum.GetValues(parameterType).Cast<object>().ToArray();
            }

            var nullableUnderlyingType = Nullable.GetUnderlyingType(parameterType);
            if (nullableUnderlyingType != null)
                return new object[] { null }.Concat(GetAllValidValues(nullableUnderlyingType)).ToArray();

            return new object[] { Activator.CreateInstance(parameterType) };
        }
    }
}