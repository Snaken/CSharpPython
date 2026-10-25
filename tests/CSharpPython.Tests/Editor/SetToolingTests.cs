using CSharpPython.Editor;
using System.Linq;

namespace CSharpPython.Tests.Editor
{
    public class SetToolingTests
    {
        private static string[] Names(string source) =>
            Completer.Complete(source, source.Length, new PyVm()).Items.Select(i => i.Text).ToArray();

        [TestCase("{1, 2}.")]
        [TestCase("set().")]
        [TestCase("s = {1}\ns.")]
        [TestCase("s = set([1])\ns.")]
        [TestCase("s = {x for x in y}\ns.")]
        public void SetMembersAreSuggested(string source)
        {
            var names = Names(source);
            Assert.That(names, Does.Contain("add"));
            Assert.That(names, Does.Contain("issubset"));
            Assert.That(names, Does.Not.Contain("append"));
        }

        [TestCase("{}.")]
        [TestCase("{1: 2}.")]
        [TestCase("{1: {2, 3}}.")]
        public void DictDisplaysStillGetDictMembers(string source)
        {
            Assert.That(Names(source), Does.Contain("items"));
            Assert.That(Names(source), Does.Not.Contain("add"));
        }

        [Test]
        public void DelIsAKeywordForCompletionAndHighlighting()
        {
            Assert.That(Names("de"), Does.Contain("del"));
            var spans = SyntaxHighlighter.Highlight("del x[0]\n");
            Assert.That(spans[0].Kind, Is.EqualTo(HighlightKind.Keyword));
            Assert.That(Analyzer.Analyze("x = [1]\ndel x[0]\ndel x\n"), Is.Empty);
        }

        [Test]
        public void SetMethodsHaveSignatures()
        {
            var help = SignatureHelper.Get("s = {1}\ns.add(", 14, new PyVm());
            Assert.That(help.Label, Is.EqualTo("add(x)"));
            Assert.That(SignatureHelper.Get("set(", 4, new PyVm()).Label, Is.EqualTo("set(iterable=())"));
            Assert.That(SignatureHelper.Get("{1}.union(", 10, new PyVm()).Label, Is.EqualTo("union(*iterables)"));
        }
    }
}
