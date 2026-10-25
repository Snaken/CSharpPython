using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    public class BlockJsonTests
    {
        private static BlockGraph Sample()
        {
            var g = new BlockGraph().AddVariable("hp", "100").AddVariable("name", "\"a \\\"b\\\" \u2028 é\"");
            var f = new BlockFunction("on_hit", "amount", "who") { Start = "sub" };
            f.Add(new BlockNode("sub", "change", "hp").Connect("value", "amount").Then("c"));
            f.Add(new BlockNode("c", "comment", "line1\nline2\ttab\u0001"));
            f.Add(new BlockNode("amount", "get", "amount"));
            g.AddFunction(f).AddFunction(new BlockFunction("empty"));
            return g;
        }

        private static void AssertSame(BlockGraph a, BlockGraph b)
        {
            Assert.That(b.ToJson(false), Is.EqualTo(a.ToJson(false)));
            Assert.That(b.Functions[0].Nodes[1].Name, Is.EqualTo(a.Functions[0].Nodes[1].Name));
            Assert.That(b.Variables[1].Initial, Is.EqualTo(a.Variables[1].Initial));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RoundTrips(bool indent)
        {
            var g = Sample();
            AssertSame(g, BlockGraph.FromJson(g.ToJson(indent)));
        }

        [Test]
        public void RoundTripCompilesTheSame()
        {
            var g = Sample();
            var back = BlockGraph.FromJson(g.ToJson());
            Assert.That(BlockCompiler.Compile(back).Source, Is.EqualTo(BlockCompiler.Compile(g).Source));
        }

        [Test]
        public void EmptyGraphAndNullsRoundTrip()
        {
            var g = new BlockGraph();
            Assert.That(BlockGraph.FromJson(g.ToJson()).Functions, Is.Empty);
            g.Variables.Add(null);
            g.Functions.Add(new BlockFunction { Nodes = null, Parameters = null });
            var back = BlockGraph.FromJson(g.ToJson());
            Assert.That(back.Variables, Is.Empty);
            Assert.That(back.Functions.Single().Name, Is.Null);
        }

        [Test]
        public void WritesCompactAndIndentedForms()
        {
            var g = new BlockGraph().AddVariable("x", "1");
            Assert.That(g.ToJson(false), Is.EqualTo("{\"Version\":1,\"Variables\":[{\"Name\":\"x\",\"Initial\":\"1\"}],\"Functions\":[]}"));
            Assert.That(g.ToJson(), Does.Contain("\n  \"Variables\": [\n"));
        }

        [Test]
        public void ReadsWhatUnityJsonUtilityWrites()
        {
            const string unity = "{\"Variables\":[{\"Name\":\"x\",\"Initial\":\"1\"}],\"Functions\":[{\"Name\":\"f\",\"Parameters\":[\"a\"],\"Start\":\"\","
                + "\"Nodes\":[{\"Id\":\"n\",\"Block\":\"get\",\"Name\":\"a\",\"Inputs\":[{\"Pin\":\"p\",\"From\":\"\",\"Literal\":\"2\"}],\"Flow\":[]}]}],\"extra\":{\"k\":[1,2.5e1,-3,true,false,null]}}";
            var g = BlockGraph.FromJson(unity);
            Assert.That(g.Version, Is.EqualTo(BlockGraph.CurrentVersion));
            Assert.That(g.Functions[0].Nodes[0].Inputs[0].Literal, Is.EqualTo("2"));
            Assert.That(g.Functions[0].Start, Is.EqualTo(""));
        }

        [Test]
        public void MissingListsAndNullsAreEmpty()
        {
            var g = BlockGraph.FromJson("{\"Variables\":null,\"Functions\":[{\"Nodes\":[{}]}]}");
            Assert.That(g.Variables, Is.Empty);
            Assert.That(g.Functions[0].Nodes[0].Inputs, Is.Empty);
            Assert.That(BlockGraph.FromJson("  {}  ").Functions, Is.Empty);
        }

        [Test]
        public void ReadsEscapes()
        {
            var g = BlockGraph.FromJson("{\"Variables\":[{\"Name\":\"\\\"\\\\\\/\\b\\f\\n\\r\\t\\u0041\\u00e9\"}]}");
            Assert.That(g.Variables[0].Name, Is.EqualTo("\"\\/\b\f\n\r\tAé"));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{")]
        [TestCase("{\"a\"")]
        [TestCase("{\"a\":")]
        [TestCase("{\"a\" 1}")]
        [TestCase("{a:1}")]
        [TestCase("{\"a\":1,}")]
        [TestCase("{\"a\":1 \"b\":2}")]
        [TestCase("{\"Functions\":[}")]
        [TestCase("{\"Functions\":[{}")]
        [TestCase("{\"Functions\":[{} {}]}")]
        [TestCase("{\"Functions\":[{},")]
        [TestCase("{} x")]
        [TestCase("{\"a\":\"abc")]
        [TestCase("{\"a\":\"ab\\")]
        [TestCase("{\"a\":\"\\q\"}")]
        [TestCase("{\"a\":\"\\u12\"}")]
        [TestCase("{\"a\":\"\\uzzzz\"}")]
        [TestCase("{\"a\":\"line\nbreak\"}")]
        [TestCase("{\"a\":-}")]
        [TestCase("{\"a\":tru}")]
        [TestCase("{\"a\":@}")]
        public void BadSyntaxIsRejected(string json)
        {
            Assert.Throws<BlockGraphFormatException>(() => BlockGraph.FromJson(json));
        }

        [TestCase("[]")]
        [TestCase("1")]
        [TestCase("\"x\"")]
        [TestCase("null")]
        [TestCase("{\"Version\":\"1\"}")]
        [TestCase("{\"Version\":1.5}")]
        [TestCase("{\"Version\":-1}")]
        [TestCase("{\"Version\":99}")]
        [TestCase("{\"Variables\":{}}")]
        [TestCase("{\"Variables\":[1]}")]
        [TestCase("{\"Variables\":[{\"Name\":1}]}")]
        [TestCase("{\"Functions\":[{\"Parameters\":[1]}]}")]
        [TestCase("{\"Functions\":[{\"Nodes\":[{\"Inputs\":[null]}]}]}")]
        [TestCase("{\"Functions\":[{\"Nodes\":[{\"Flow\":[\"x\"]}]}]}")]
        public void BadShapeIsRejected(string json)
        {
            Assert.Throws<BlockGraphFormatException>(() => BlockGraph.FromJson(json));
        }

        [Test]
        public void DeepNestingIsRejectedNotAStackOverflow()
        {
            string deep = new string('[', 100000) + new string(']', 100000);
            var ex = Assert.Throws<BlockGraphFormatException>(() => BlockGraph.FromJson(deep));
            Assert.That(ex.Message, Does.Contain("deeply"));
        }

        [Test]
        public void NullTextThrows()
        {
            Assert.Throws<System.ArgumentNullException>(() => BlockGraph.FromJson(null));
        }

        [Test]
        public void FuzzedDocumentsNeverCrash()
        {
            var rng = new System.Random(7);
            string text = Sample().ToJson(false);
            for (int i = 0; i < 3000; i++)
            {
                var chars = text.ToCharArray();
                for (int k = rng.Next(1, 4); k > 0; k--) chars[rng.Next(chars.Length)] = "{}[]\",:\\u1 x"[rng.Next(12)];
                string s = new string(chars);
                if (rng.Next(4) == 0) s = s.Substring(0, rng.Next(s.Length));
                try { BlockGraph.FromJson(s); }
                catch (BlockGraphFormatException) { }
            }
        }
    }
}
