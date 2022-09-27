using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using ApprovalTests;
using ApprovalTests.Namers;
using ApprovalTests.Reporters;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Tests.Mocks;
using BenchmarkDotNet.Tests.XUnit;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using JetBrains.Annotations;
using Xunit;

namespace BenchmarkDotNet.Tests.Columns
{
    // In case of failed approval tests, use the following reporter:
    // [UseReporter(typeof(KDiffReporter))]
    [UseReporter(typeof(XUnit2Reporter))]
    [UseApprovalSubdirectory("ApprovedFiles")]
    [Collection("ApprovalTests")]
    public class JobColumnsApprovalTests : IDisposable
    {
        private readonly CultureInfo initCulture;

        public JobColumnsApprovalTests()
        {
            initCulture = Thread.CurrentThread.CurrentCulture;
        }

        [UsedImplicitly]
        public static TheoryData<string, IConfig> GetConfigs()
        {
            var data = new TheoryData<string, IConfig>
            {
                {
                    "Default",
                    DefaultConfig.Instance
                        .AddJob(Job.Default)
                },
                {
                    "InProcess",
                    DefaultConfig.Instance
                        .AddJob(Job.InProcess)
                },
                {
                    "CustomJobId",
                    DefaultConfig.Instance
                        .AddJob(new Job("CustomJobId"))
                },
                {
                    "Default Dry",
                    DefaultConfig.Instance
                        .AddJob(Job.Default)
                        .AddJob(Job.Dry)
                },
                {
                    "Default InProcess",
                    DefaultConfig.Instance
                        .AddJob(Job.Default)
                        .AddJob(Job.InProcess)
                },
                {
                    "Default CustomJobId",
                    DefaultConfig.Instance
                        .AddJob(Job.Default)
                        .AddJob(new Job("CustomJobId"))
                },
                {
                    "Default InProcessEmitToolchain",
                    DefaultConfig.Instance
                        .AddJob(Job.Default)
                        .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance))
                },
                {
                    "Default Core60",
                    DefaultConfig.Instance
                        .AddJob(Job.Default)
                        .AddJob(Job.Default.WithRuntime(CoreRuntime.Core60))
                },
                {
                    "Dry.Core60 Dry.Core60.Core60Toolchain",
                    DefaultConfig.Instance
                        .AddJob(Job.Dry.WithRuntime(CoreRuntime.Core60))
                        .AddJob(Job.Dry.WithRuntime(CoreRuntime.Core60).WithToolchain(CsProjCoreToolchain.NetCoreApp60))
                },
                {
                    "Default Core60 NativeAOT60 Net48",
                    DefaultConfig.Instance
                        .AddJob(Job.Default)
                        .AddJob(Job.Default.WithRuntime(CoreRuntime.Core60))
                        .AddJob(Job.Default.WithRuntime(NativeAotRuntime.Net60))
                        .AddJob(Job.Default.WithRuntime(ClrRuntime.Net48))
                },
                {
                    "Default Core60.Toolchain NativeAOT60 Net48",
                    DefaultConfig.Instance
                        .AddJob(Job.Default)
                        .AddJob(Job.Default.WithRuntime(CoreRuntime.Core60).WithToolchain(CsProjCoreToolchain.NetCoreApp60))
                        .AddJob(Job.Default.WithRuntime(NativeAotRuntime.Net60))
                        .AddJob(Job.Default.WithRuntime(ClrRuntime.Net48))
                },
                {
                    "InProcessEmit InProcessNoEmit Toolchains",
                    DefaultConfig.Instance
                        .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance))
                        .AddJob(Job.Default.WithToolchain(InProcessNoEmitToolchain.Instance))
                },
                {
                    "Net48 Core60",
                    DefaultConfig.Instance
                        .AddJob(Job.Default.WithRuntime(ClrRuntime.Net48))
                        .AddJob(Job.Default.WithRuntime(CoreRuntime.Core60))
                },
                {
                    "Net60 NativeAOT60",
                    DefaultConfig.Instance
                        .AddJob(Job.Default.WithRuntime(CoreRuntime.Core60))
                        .AddJob(Job.Default.WithRuntime(NativeAotRuntime.Net60))
                },
            };

            return data;
        }

        [TheoryNetCoreOnly("Job.Default is different in .NET Framework")]
        [MemberData(nameof(GetConfigs))]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ColumnsDisplayTest(string jobsName, IConfig config)
        {
            NamerFactory.AdditionalInformation = jobsName;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            var logger = new AccumulationLogger();
            logger.WriteLine("=== " + jobsName + " ===");

            var exporter = MarkdownExporter.Mock;
            var summary = MockFactory.CreateSummary<BenchmarkClass>(config);
            exporter.ExportToLog(summary, logger);

            var log = logger.GetLog();
            log = ReplaceRandomIDs(log);
            Approvals.Verify(log);
        }

        private static string ReplaceRandomIDs(string log)
        {
            var regex = new Regex(@"Job-\w*");

            var index = 0;
            foreach (Match match in regex.Matches(log))
            {
                var randomGeneratedJobName = match.Value;

                // JobIdGenerator.GenerateRandomId() generates Job-ABCDEF
                // respect the length for proper table formatting
                var persistantName = $"Job-rndId{index}";
                log = log.Replace(randomGeneratedJobName, persistantName);
                index++;
            }

            return log;
        }

        public void Dispose() => Thread.CurrentThread.CurrentCulture = initCulture;

        public class BenchmarkClass
        {
            [Benchmark] public void Method() { }
        }
    }
}