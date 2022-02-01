using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Extensions;
using BenchmarkDotNet.Portability;
using Microsoft.CSharp;

namespace BenchmarkDotNet.Running
{
    public static partial class BenchmarkConverter
    {
        public static BenchmarkRunInfo[] UrlToBenchmarks(string url, IConfig config = null)
        {
            if (!RuntimeInformation.IsFullFramework)
                throw new NotSupportedException("Supported only on Full .NET Framework.");

            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidBenchmarkDeclarationException("URL is empty.");

            url = GetRawUrl(url);
            string benchmarkContent;
            try
            {
                var webRequest = WebRequest.Create(url);
                using (var response = webRequest.GetResponse())
                using (var content = response.GetResponseStream())
                {
                    if (content == null)
                        throw new InvalidBenchmarkDeclarationException("Response content is empty.");

                    using (var reader = new StreamReader(content))
                        benchmarkContent = reader.ReadToEnd();

                    if (string.IsNullOrWhiteSpace(benchmarkContent))
                        throw new InvalidBenchmarkDeclarationException($"Content of '{url}' is empty.");
                }
            }
            catch (Exception e)
            {
                throw new InvalidBenchmarkDeclarationException(e.Message);
            }
            return SourceToBenchmarks(benchmarkContent, config);
        }

        public static BenchmarkRunInfo[] SourceToBenchmarks(string source, IConfig config = null)
        {
            if (!RuntimeInformation.IsFullFramework)
                throw new NotSupportedException("Supported only on Full .NET Framework.");

            if (string.IsNullOrEmpty(source))
                throw new InvalidBenchmarkDeclarationException("Source isn't provided.");

            string benchmarkContent = source;
            CompilerResults compilerResults;
            using (var cSharpCodeProvider = new CSharpCodeProvider())
            {
                string directoryName = Path.GetDirectoryName(typeof(BenchmarkCase).Assembly.Location)
                                       ?? throw new DirectoryNotFoundException(typeof(BenchmarkCase).Assembly.Location);
                var compilerParameters = new CompilerParameters(
                    new[]
                    {
                        "mscorlib.dll",
                        "System.dll",
                        "System.Core.dll"
                    })
                {
                    CompilerOptions = "/unsafe /optimize",
                    GenerateInMemory = false,
                    OutputAssembly = Path.Combine(
                        directoryName,
                        $"{Path.GetFileNameWithoutExtension(Path.GetTempFileName())}.dll")
                };

                compilerParameters.ReferencedAssemblies.Add(typeof(BenchmarkCase).Assembly.Location);
                compilerResults = cSharpCodeProvider.CompileAssemblyFromSource(compilerParameters, benchmarkContent);
            }

            if (compilerResults.Errors.HasErrors)
            {
                var message = string.Join("\n", compilerResults.Errors.Cast<CompilerError>().Select(error => error.ErrorText));
                throw new InvalidBenchmarkDeclarationException(message);
            }

            var benchmarkTypes = compilerResults.CompiledAssembly.GetRunnableBenchmarks();

            var resultBenchmarks = new List<BenchmarkRunInfo>();
            foreach (var type in benchmarkTypes)
            {
                var runInfo = TypeToBenchmarks(type, config);
                var benchmarks = runInfo.BenchmarksCases.Select(b =>
                {
                    var target = b.Descriptor;
                    return BenchmarkCase.Create(
                        new Descriptor(target.Type, target.WorkloadMethod, target.GlobalSetupMethod, target.GlobalCleanupMethod,
                            target.IterationSetupMethod, target.IterationCleanupMethod,
                            target.WorkloadMethodDisplayInfo, benchmarkContent, target.Baseline, target.Categories, target.OperationsPerInvoke),
                        b.Job,
                        b.Parameters,
                        b.Config);
                });
                resultBenchmarks.Add(
                    new BenchmarkRunInfo(benchmarks.ToArray(), runInfo.Type, runInfo.Config));
            }

            return resultBenchmarks.ToArray();
        }

        private static string GetRawUrl(string url)
        {
            if (url.StartsWith("https://gist.github.com/") && !(url.EndsWith("/raw") || url.EndsWith("/raw/")))
                return url.TrimEnd('/') + "/raw";
            if (url.StartsWith("https://github.com/") && url.Contains("/blob/"))
                return url.Replace("https://github.com/", "https://raw.githubusercontent.com/").Replace("/blob/", "/");
            return url;
        }
    }
}
