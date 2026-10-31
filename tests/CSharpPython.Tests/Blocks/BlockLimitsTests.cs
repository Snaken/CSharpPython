using System;
using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // A file that goes over the host's limits is rejected on load, with the reason (docs/node-view-design.md, decision 19).
    public class BlockLimitsTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        private static BlockGraph Graph()
        {
            var main = new BlockFunction("main", "a", "b") { Start = "p" };
            main.Add(N("p", "call", "print").Connect("arg0", "l").Literal("arg1", "\"hello\"").Then("n"));
            main.Add(N("n", "comment", "a long enough comment text"));
            main.Add(N("l", "list").Literal("item0", "1").Literal("item1", "2").Literal("item2", "3"));
            var g = new BlockGraph().AddFunction(main).AddFunction(new BlockFunction("helper"));
            g.AddVariable("score", "10", "int");
            g.Editor.Place("p", 1, 2);
            g.Editor.Comments.Add(new BlockCommentBox("a box of text", 0, 0, 1, 1));
            var bends = new BlockWireBends("p", "arg0");
            bends.Points.Add(new BlockPoint(1, 1));
            g.Editor.Wires.Add(bends);
            g.Editor.Groups.Add(new BlockGroup("setup", "p", "n"));
            return g;
        }

        private static string Rejected(BlockGraph g, BlockLimits limits) => Rejected(g.ToJson(), limits);

        private static string Rejected(string json, BlockLimits limits)
        {
            var e = Assert.Throws<BlockGraphFormatException>(() => BlockGraph.FromJson(json, limits));
            return e.Message;
        }

        // ---- Defaults ----

        [Test]
        public void AnOrdinaryGraphIsWithinTheDefaults()
        {
            Assert.That(new BlockLimits().Problem(Graph()), Is.Null);
            Assert.That(BlockGraph.FromJson(Graph().ToJson()).Functions, Has.Count.EqualTo(2));
            Assert.That(BlockGraph.FromJson(Graph().ToJson(), new BlockLimits()).Functions, Has.Count.EqualTo(2));
        }

        [Test]
        public void ALimitCannotBeNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BlockLimits { MaxNodes = -1 });
            Assert.That(new BlockLimits { MaxNodes = 0 }.MaxNodes, Is.EqualTo(0));
        }

        [Test]
        public void ANullGraphIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new BlockLimits().Problem(null));
        }

        // ---- Each limit ----

        [Test]
        public void TheDocumentLengthIsChecked()
        {
            string json = Graph().ToJson();
            string message = Rejected(json, new BlockLimits { MaxTextLength = json.Length - 1 });
            Assert.That(message, Does.Contain("document is too long").And.Contain("limit is " + (json.Length - 1)));
            Assert.That(BlockGraph.FromJson(json, new BlockLimits { MaxTextLength = json.Length }), Is.Not.Null);
        }

        [Test]
        public void TheLimitIsCheckedBeforeTheDocumentIsRead()
        {
            // Not valid JSON, but too long first.
            Assert.That(Rejected("{" + new string('x', 50), new BlockLimits { MaxTextLength = 10 }), Does.Contain("too long"));
        }

        [Test]
        public void CountsAreChecked()
        {
            Assert.That(Rejected(Graph(), new BlockLimits { MaxFunctions = 1 }), Does.Contain("too many functions (2; the limit is 1)"));
            Assert.That(Rejected(Graph(), new BlockLimits { MaxVariables = 0 }), Does.Contain("too many variables"));
            Assert.That(Rejected(Graph(), new BlockLimits { MaxNodesPerFunction = 2 }), Does.Contain("'main' has too many blocks (3; the limit is 2)"));
            Assert.That(Rejected(Graph(), new BlockLimits { MaxNodes = 2 }), Does.Contain("too many blocks (3; the limit is 2)"));
            Assert.That(Rejected(Graph(), new BlockLimits { MaxParameters = 1 }), Does.Contain("too many parameters"));
            Assert.That(Rejected(Graph(), new BlockLimits { MaxInputsPerNode = 2 }), Does.Contain("block 'l'").And.Contain("too many inputs"));
            Assert.That(Rejected(Graph(), new BlockLimits { MaxLinksPerNode = 0 }), Does.Contain("block 'p'").And.Contain("too many execution links"));
        }

        [Test]
        public void TextLengthsAreChecked()
        {
            Assert.That(Rejected(Graph(), new BlockLimits { MaxIdLength = 0 }), Does.Contain("the start of the function 'main' is too long"));
            Assert.That(Rejected(Graph(), new BlockLimits { MaxNameLength = 3 }), Does.Contain("name of a variable is too long (5 characters; the limit is 3)"));
            var f = new BlockGraph().AddFunction(new BlockFunction("main"));
            Assert.That(Rejected(f, new BlockLimits { MaxNameLength = 3 }), Does.Contain("name of a function is too long (4 characters; the limit is 3)"));
            Assert.That(Rejected(Graph(), new BlockLimits { MaxPinLength = 3 }), Does.Contain("pin name").And.Contain("too long"));
            Assert.That(Rejected(Graph(), new BlockLimits { MaxCommentLength = 5 }), Does.Contain("the text of the block 'n'"));
            Assert.That(Rejected(Graph(), new BlockLimits { MaxLiteralLength = 3 }), Does.Contain("too long"));
            Assert.That(Rejected(Graph(), new BlockLimits { MaxTypeLength = 2 }), Does.Contain("the type of variable 'score' is too long"));
        }

        [Test]
        public void TheStartingValueAndTypedInValuesAreChecked()
        {
            var g = new BlockGraph().AddFunction(new BlockFunction("main"));
            g.AddVariable("v", new string('1', 20), "int");
            Assert.That(Rejected(g, new BlockLimits { MaxLiteralLength = 10 }), Does.Contain("starting value of variable 'v'"));
            var f = new BlockFunction("main");
            f.Add(N("a", "call", "print").Literal("arg0", new string('"', 20)));
            Assert.That(Rejected(new BlockGraph().AddFunction(f), new BlockLimits { MaxLiteralLength = 10 }), Does.Contain("typed-in value of the block 'a'"));
        }

        [Test]
        public void ACommentIsAllowedMoreThanAName()
        {
            var f = new BlockFunction("main");
            f.Add(N("c", "comment", new string('x', 500)));
            var g = new BlockGraph().AddFunction(f);
            Assert.That(BlockGraph.FromJson(g.ToJson(), new BlockLimits { MaxNameLength = 100 }), Is.Not.Null);
            f.Nodes[0].Block = "set";
            Assert.That(Rejected(g, new BlockLimits { MaxNameLength = 100 }), Does.Contain("the name of the block 'c'"));
        }

        [Test]
        public void TheEditorSectionIsChecked()
        {
            Assert.That(Rejected(Graph(), new BlockLimits { MaxEditorEntries = 0 }), Does.Contain("editor section"));
            var g = Graph();
            g.Editor.Wires[0].Points.Add(new BlockPoint(2, 2));
            Assert.That(Rejected(g, new BlockLimits { MaxEditorEntries = 1 }), Does.Contain("reroute points"));
            Assert.That(Rejected(Graph(), new BlockLimits { MaxEditorTextLength = 3 }), Does.Contain("comment box text"));
            var many = Graph();
            many.Editor.Groups[0].Nodes.AddRange(new[] { "a", "b", "c" });
            Assert.That(Rejected(many, new BlockLimits { MaxEditorEntries = 4 }), Does.Contain("blocks in groups"));
        }

        [Test]
        public void ALongNameDoesNotMakeALongMessage()
        {
            var g = new BlockGraph().AddFunction(new BlockFunction(new string('f', 5000)));
            string message = Rejected(g, new BlockLimits { MaxNameLength = 10 });
            Assert.That(message.Length, Is.LessThan(200));
            var f = new BlockFunction(new string('f', 30));
            f.Add(N(new string('i', 100), "comment"));
            Assert.That(Rejected(new BlockGraph().AddFunction(f), new BlockLimits { MaxIdLength = 10 }).Length, Is.LessThan(200));
        }

        [Test]
        public void GraphsBuiltInCodeCanBeCheckedToo()
        {
            var g = new BlockGraph().AddFunction(new BlockFunction("main"));
            Assert.That(new BlockLimits { MaxFunctions = 0 }.Problem(g), Does.Contain("too many functions"));
            Assert.That(new BlockLimits().Problem(g), Is.Null);
        }

        [Test]
        public void AFlatDocumentIsStillRejectedWhenItIsTooDeep()
        {
            string deep = new string('[', 100) + new string(']', 100);
            Assert.Throws<BlockGraphFormatException>(() => BlockGraph.FromJson("{\"Variables\":" + deep + "}"));
        }
    }
}
