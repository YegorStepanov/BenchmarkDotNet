using System;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace BenchmarkDotNet.Samples
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var cng = DefaultConfig.Instance.WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(40)).WithOptions(ConfigOptions.KeepBenchmarkFiles);
            BenchmarkRunner.Run<MyBenchmark>(cng, args: "-j dry".Split()); //new DebugInProcessConfig()
        }

        private static void NewMethod(string abc)
        {
            // Console.WriteLine("1:" + abc.EscapeSpecialCharacters()); //!

            // Console.WriteLine("2:" + abc.EscapeSpecialCharacters2());
            Console.WriteLine("\n");

            //  var ll = @"\";
            //
            //  var aa = $@"{a}";
            //
            //  var z = $"aaa{{ll}";
            // SymbolDisplay.
            //  var infoLine = "";
            //  var q = $@"\""";
        }
    }

    [LogicalGroupColumn]
    [BaselineColumn]
    public class MethodBaseline_MethodsParamsEscaped2
    {
        [Params("Should|Escape", "Should|Escape1")]
        public string Param;

        [Benchmark(Baseline = true)] public void Base() { }
        [Benchmark] public void Foo() { }
        [Benchmark] public void Bar() { }
    }

    // NewMethod("abc");
    // NewMethod("a\nb");
    // NewMethod("a\ab\acde");
    // NewMethod("\"");
    // NewMethod("\\");
    // NewMethod("\0");
    // NewMethod("\a");
    // NewMethod("\b");
    // NewMethod("\f");
    // NewMethod("\n");
    // NewMethod("\t");
    // NewMethod("\v");
    // NewMethod("\u0061");
    // NewMethod("\x0[0][6][1]");
    // NewMethod("\U00000061");
    // NewMethod("\xA1");
    // NewMethod("\xA1A");
    // NewMethod("\x00A1");
    // NewMethod("C:\\files.txt");
    // NewMethod(@"C:\files.txt");
    // NewMethod("\"");
    // NewMethod("\"\"");
    // NewMethod(@"""""");

    // [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1028:Code should not contain trailing whitespace")]
    public class MyBenchmark
    {
        //what if we apple Escape 5 times?
        [Params(
            "\t\t\t\a\a\a",
            "\t oh noo \t",
            "\n\n\n\n\n\n",
            @"\t\t\t\a\a\a",
            @"\t oh noo \t",
            @"\n\n\n\n\n\n",
            @"""",
            nameof(MyBenchmark)
            // "\0",
            // "\a",
            // "\a",
            // "\b",
            // "\t",
            // "\tab",
            // "\n",
            // "\f",
            // "\"",
            // "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
            // "C:\files.txt",
            // @"C:\files.txt",
            // "\\",
            // "\\0",
            // "\u0061",
            // "\x0061",
            // "\x61",
            // "a\tb",
            // "abcdefghijklmnopqrstuvwxyz",
            // "a|b",
            // "a|b"
        )]
        public string MySuperParameter;

        [Benchmark] public void MyMethod()
        {
            Console.WriteLine("O2PA:" + MySuperParameter + "END, " + MySuperParameter.Length);

            // Thread.Sleep(4000);

            if (MySuperParameter == "\a")
            {
                Console.WriteLine("sleep:" + 500);
                Thread.Sleep(500);
            }

            if (MySuperParameter == "\0")
            {
                Console.WriteLine("sleep:" + 1000);
                Thread.Sleep(1500);
            }

            if (MySuperParameter == "\\0")
            {
                Console.WriteLine("sleep:" + 2000);
                Thread.Sleep(2000);
            }

            if (MySuperParameter == "\\t")
            {
                Console.WriteLine("sleep:" + 3000);
                Thread.Sleep(3000);
            }
        }
    }
}