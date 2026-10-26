using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests
{
    /// <summary>
    /// Runs every <c>Scripts/**/*.py</c> file and compares what it prints with the <c>.expected</c> file beside it.
    /// The expected output comes from CPython: regenerate it with <c>python3 scripts/update-expected.py</c>.
    /// </summary>
    public class ScriptFileTests
    {
        private static string Root => Path.Combine(AppContext.BaseDirectory, "Scripts");

        public static IEnumerable<TestCaseData> Scripts() =>
            Directory.GetFiles(Root, "*.py", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => new TestCaseData(p).SetName(Path.GetRelativePath(Root, p).Replace('\\', '/')));

        [TestCaseSource(nameof(Scripts))]
        public void PrintsWhatCpythonPrints(string path)
        {
            string expected = File.ReadAllText(Path.ChangeExtension(path, ".expected"));
            Assert.That(Out(File.ReadAllText(path)), Is.EqualTo(expected.TrimEnd('\n')));
        }

        [Test]
        public void EveryScriptHasAnExpectedFile()
        {
            foreach (string path in Directory.GetFiles(Root, "*.py", SearchOption.AllDirectories))
                Assert.That(File.Exists(Path.ChangeExtension(path, ".expected")), path);
        }
    }
}
