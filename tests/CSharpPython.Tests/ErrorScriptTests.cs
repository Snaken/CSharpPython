using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests
{
    /// <summary>
    /// Each <c>Scripts/errors/*.py</c> file holds short snippets separated by a line <c># ---</c>, and the <c>.expected</c>
    /// file beside it names, one line for each snippet, the exception CPython raises when it is run. Every snippet must
    /// fail in CSharpPython with the same type. Regenerate the expected files with <c>python3 scripts/update-expected.py errors</c>.
    /// </summary>
    public class ErrorScriptTests
    {
        private static string Root => Path.Combine(AppContext.BaseDirectory, "Scripts", "errors");

        private static string[] Split(string text) => text.Replace("\r\n", "\n").Split(new[] { "\n# ---\n" }, StringSplitOptions.None);

        public static IEnumerable<TestCaseData> Snippets()
        {
            foreach (string path in Directory.GetFiles(Root, "*.py").OrderBy(p => p, StringComparer.Ordinal))
            {
                string[] snippets = Split(File.ReadAllText(path));
                string[] types = File.ReadAllText(Path.ChangeExtension(path, ".expected")).Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
                Assert.That(types.Length, Is.EqualTo(snippets.Length), path + ": one expected type for each snippet");
                for (int i = 0; i < snippets.Length; i++)
                    yield return new TestCaseData(snippets[i], types[i]).SetName("errors/" + Path.GetFileName(path) + " #" + (i + 1));
            }
        }

        [TestCaseSource(nameof(Snippets))]
        public void FailsWithTheExceptionCpythonRaises(string snippet, string type) =>
            Assert.That(Fails(snippet).TypeName, Is.EqualTo(type), snippet);
    }
}
