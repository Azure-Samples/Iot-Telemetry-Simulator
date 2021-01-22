namespace IotTelemetrySimulator.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using Xunit;
    using Xunit.Abstractions;

    public class PerformanceTest
    {
        private readonly ITestOutputHelper testOutputHelper;

        public PerformanceTest(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        public string GetTestString()
        {
            var str = "abcdefgi";
            var dctKeys = this.GetDictionary().Select(kv => kv.Key).ToList();
            foreach (var variable in dctKeys)
            {
                str += ",prefixasdadsaadssasadprefix$." + variable + "anothersuffix";
            }

            return str + str;
        }

        public Dictionary<string, string> GetDictionary()
        {
            var dct = new Dictionary<string, string>();
            for (int i = 0; i < 10; ++i)
            {
                dct["MyVariable" + i + "suffix"] = (i * 7).ToString();
            }

            return dct;
        }

        [Fact]
        public void TestInterleavedApproach()
        {
            var str = this.GetTestString();
            var translate = this.GetDictionary();

            var futureVariableNames = translate.Select(kv => kv.Key);

            var chunks = new List<object> { str };

            foreach (var key in futureVariableNames.OrderByDescending(s => s.Length))
            {
                Func<Dictionary<string, string>, string> substituteFunc = translations => translations[key];

                chunks = chunks.SelectMany(chunk =>
                {
                    if (chunk is string stringChunk)
                    {
                        var parts = stringChunk.Split("$." + key);
                        if (parts.Length > 1)
                        {
                            return parts.SelectMany(p => new List<object> { p, substituteFunc }).SkipLast(1);
                        }
                    }

                    return new[] { chunk };
                }).ToList();
            }

            this.testOutputHelper.WriteLine($"Template length: {str.Length} bytes");
            this.testOutputHelper.WriteLine($"Variable count:  {translate.Count}");
            this.testOutputHelper.WriteLine("---");

            var sw = new Stopwatch();
            sw.Start();
            var result = string.Join(
                string.Empty,
                chunks.Select(chunk => chunk is Func<Dictionary<string, string>, string> token
                    ? token(translate)
                    : chunk.ToString()));
            sw.Stop();

            this.testOutputHelper.WriteLine("Elapsed={0}ms", sw.Elapsed.Milliseconds);
            this.testOutputHelper.WriteLine($"Result prefix: {result.Substring(0, 30)}");
            this.testOutputHelper.WriteLine($"Result suffix: {result.Substring(result.Length - 30, 30)}");
            this.testOutputHelper.WriteLine($"Result length: {result.Length}");
            this.testOutputHelper.WriteLine($"Result hash:   {StringToSha256(result)}");
        }

        [Fact]
        public void TestRegex()
        {
            var str = this.GetTestString();
            var translate = this.GetDictionary();

            this.testOutputHelper.WriteLine($"Template length: {str.Length} bytes");
            this.testOutputHelper.WriteLine($"Variable count:  {translate.Count}");
            this.testOutputHelper.WriteLine("---");

            var regexStr = @"\$\.(" + string.Join("|", translate.Select(kv => kv.Key)) + ")";
            Regex re = new Regex(regexStr, RegexOptions.Compiled);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            var result = re.Replace(str, match => translate[match.Groups[1].Value]);
            sw.Stop();

            this.testOutputHelper.WriteLine("Elapsed={0}ms", sw.Elapsed.Milliseconds);
            this.testOutputHelper.WriteLine($"Result prefix: {result.Substring(0, 30)}");
            this.testOutputHelper.WriteLine($"Result suffix: {result.Substring(result.Length - 30, 30)}");
            this.testOutputHelper.WriteLine($"Result length: {result.Length}");
            this.testOutputHelper.WriteLine($"Result hash:   {StringToSha256(result)}");
        }

        [Fact]
        public void TestReplace()
        {
            var str = this.GetTestString();
            var translate = this.GetDictionary();

            this.testOutputHelper.WriteLine($"Template length: {str.Length} bytes");
            this.testOutputHelper.WriteLine($"Variable count:  {translate.Count}");
            this.testOutputHelper.WriteLine("---");

            Stopwatch sw = new Stopwatch();
            sw.Start();
            foreach (var kv in translate)
            {
                str = str.Replace("$." + kv.Key, kv.Value);
            }

            sw.Stop();

            var result = str;
            this.testOutputHelper.WriteLine("Elapsed={0}ms", sw.Elapsed.Milliseconds);
            this.testOutputHelper.WriteLine($"Result prefix: {result.Substring(0, 30)}");
            this.testOutputHelper.WriteLine($"Result suffix: {result.Substring(result.Length - 30, 30)}");
            this.testOutputHelper.WriteLine($"Result length: {result.Length}");
            this.testOutputHelper.WriteLine($"Result hash:   {StringToSha256(result)}");
        }

        public static string StringToSha256(string s)
        {
            using var alg = SHA256.Create();
            return string.Join(null, alg.ComputeHash(Encoding.UTF8.GetBytes(s)).Select(x => x.ToString("x2")));
        }
    }
}
