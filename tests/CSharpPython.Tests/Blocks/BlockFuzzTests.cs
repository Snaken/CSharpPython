using System;
using System.Collections.Generic;
using System.Linq;
using CSharpPython.Blocks;
using CSharpPython.Editor;

namespace CSharpPython.Tests.Blocks
{
    public class BlockFuzzTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        // Builds a graph that uses most block kinds, so mutations hit many code paths.
        private static BlockGraph Sample()
        {
            var g = new BlockGraph().AddVariable("total", "0").AddVariable("label", "\"x\"");
            var helper = new BlockFunction("helper", "n");
            helper.Add(N("h1", "if").Connect("cond", "h2").Then("then", "h3").Then("next", "h5"));
            helper.Add(N("h2", "lt").Connect("a", "h2n").Literal("b", "0"));
            helper.Add(N("h2n", "get", "n"));
            helper.Add(N("h3", "return").Literal("value", "0"));
            helper.Add(N("h5", "return").Connect("value", "h6"));
            helper.Add(N("h6", "mul").Connect("a", "h2n").Literal("b", "2"));
            helper.Start = "h1";

            var main = new BlockFunction("main");
            main.Add(N("m1", "set", "xs").Connect("value", "m2").Then("m3"));
            main.Add(N("m2", "list").Literal("item0", "1").Literal("item1", "2"));
            main.Add(N("m3", "for_each", "v").Connect("items", "m4").Then("body", "m5").Then("m9"));
            main.Add(N("m4", "get", "xs"));
            main.Add(N("m5", "call", "helper").Connect("arg0", "m6").Then("m7"));
            main.Add(N("m6", "get", "v"));
            main.Add(N("m7", "change", "total").Connect("value", "m5").Then("m8"));
            main.Add(N("m8", "append").Connect("list", "m4").Connect("value", "m5"));
            main.Add(N("m9", "repeat").Literal("count", "2").Then("body", "m10").Then("m11"));
            main.Add(N("m10", "call", "print").Connect("arg0", "m12"));
            main.Add(N("m12", "call", "max").Connect("arg0", "m13").Literal("arg1", "1"));
            main.Add(N("m13", "index").Connect("list", "m4").Literal("index", "0"));
            main.Add(N("m11", "while").Connect("cond", "m14").Then("body", "m15"));
            main.Add(N("m14", "and").Literal("a", "False").Connect("b", "m16"));
            main.Add(N("m16", "not").Literal("a", "True"));
            main.Add(N("m15", "set_index").Connect("list", "m4").Literal("index", "0").Literal("value", "-1.5"));
            main.Start = "m1";
            g.AddFunction(helper).AddFunction(main);
            return g;
        }

        private static readonly string[] Pool =
        {
            null, "", "x", "m1", "m5", "h1", "next", "then", "else", "body", "value", "a", "b", "arg0", "arg1", "cond",
            "set", "if", "call", "print", "helper", "main", "get", "literal", "list", "item0", "1", "\"s\"", "-2", "True",
            "break", "while", "a b", "_b1", "1+1", "[1]", "xs", "total",
        };

        private static void Mutate(BlockGraph g, Random rng)
        {
            string pick() => Pool[rng.Next(Pool.Length)];
            var nodes = g.Functions.SelectMany(f => f.Nodes).ToList();
            int edits = 1 + rng.Next(4);
            for (int e = 0; e < edits; e++)
            {
                var node = nodes[rng.Next(nodes.Count)];
                switch (rng.Next(12))
                {
                    case 0: node.Id = pick(); break;
                    case 1: node.Block = pick(); break;
                    case 2: node.Name = pick(); break;
                    case 3: if (node.Inputs.Count > 0) node.Inputs[rng.Next(node.Inputs.Count)].Pin = pick(); break;
                    case 4: if (node.Inputs.Count > 0) node.Inputs[rng.Next(node.Inputs.Count)].From = pick(); break;
                    case 5: if (node.Inputs.Count > 0) node.Inputs[rng.Next(node.Inputs.Count)].Literal = pick(); break;
                    case 6: if (node.Flow.Count > 0) node.Flow[rng.Next(node.Flow.Count)].To = pick(); break;
                    case 7: if (node.Flow.Count > 0) node.Flow[rng.Next(node.Flow.Count)].Pin = pick(); break;
                    case 8: g.Functions[rng.Next(g.Functions.Count)].Nodes.RemoveAll(n => n == node); break;
                    case 9: g.Functions[rng.Next(g.Functions.Count)].Start = pick(); break;
                    case 10: node.Inputs.Add(new BlockInput(pick(), pick(), pick())); break;
                    default: node.Flow.Add(new BlockLink(pick(), pick())); break;
                }
            }
        }

        [Test]
        public void UnmutatedSampleCompilesAndIsValidPython()
        {
            var r = BlockCompiler.Compile(Sample());
            Assert.That(r.Errors.Select(e => e.ToString()), Is.Empty);
            Assert.That(new PyVm().Compile(r.Source), Is.Not.Null);
            Assert.That(Analyzer.Analyze(r.Source), Is.Empty);
            Assert.That(r.Diagnostics, Is.Empty);
        }

        [Test]
        public void DamagedGraphsNeverThrowAndSuccessMeansValidPython()
        {
            var rng = new Random(20240607);
            var vm = new PyVm();
            int succeeded = 0;
            for (int i = 0; i < 4000; i++)
            {
                var g = Sample();
                Mutate(g, rng);
                BlockCompileResult r = null;
                Assert.DoesNotThrow(() => r = BlockCompiler.Compile(g), "iteration " + i);
                if (r.Succeeded)
                {
                    succeeded++;
                    Assert.That(r.Errors, Is.Empty, "iteration " + i);
                    Assert.DoesNotThrow(() => vm.Compile(r.Source), "iteration " + i + " produced:\n" + r.Source);
                    Assert.That(r.Map.LineCount, Is.EqualTo(r.Source.Split('\n').Length - 1));
                }
                else
                {
                    Assert.That(r.Errors.Any(), Is.True, "iteration " + i);
                    Assert.That(r.Source, Is.Null);
                }
            }
            // Many mutations are harmless (a literal swapped for another literal), so some graphs must still compile.
            Assert.That(succeeded, Is.GreaterThan(100));
            Assert.That(succeeded, Is.LessThan(3900));
        }
    }
}
