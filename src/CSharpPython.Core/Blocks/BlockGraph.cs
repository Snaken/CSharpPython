using System;
using System.Collections.Generic;

namespace CSharpPython.Blocks
{
    // The graph model is plain data (public fields, lists, strings) so that Unity's JsonUtility, a ScriptableObject
    // or any other serializer can store it without help. Nodes refer to each other by id.

    /// <summary>
    /// A value arriving at one input pin of a block: either the output of another block (<see cref="From"/>) or a
    /// typed-in literal (<see cref="Literal"/>, a single Python literal such as <c>42</c>, <c>"hi"</c> or <c>None</c>).
    /// </summary>
    [Serializable]
    public sealed class BlockInput
    {
        public string Pin;
        public string From;

        /// <summary>
        /// Which output of <see cref="From"/> this input reads, for a block with several outputs (the name from its
        /// description, such as <c>hit</c>). Empty for a block with one output.
        /// </summary>
        public string FromPin;

        public string Literal;

        public BlockInput() { }

        public BlockInput(string pin, string from, string literal, string fromPin = null)
        {
            Pin = pin;
            From = from;
            Literal = literal;
            FromPin = fromPin;
        }
    }

    /// <summary>An execution link: when the block finishes the pin named <see cref="Pin"/>, flow continues at <see cref="To"/>.</summary>
    [Serializable]
    public sealed class BlockLink
    {
        public string Pin;
        public string To;

        public BlockLink() { }

        public BlockLink(string pin, string to)
        {
            Pin = pin;
            To = to;
        }
    }

    /// <summary>One block placed in a function. <see cref="Block"/> is the block kind (see <see cref="BlockLibrary"/>).</summary>
    [Serializable]
    public sealed class BlockNode
    {
        /// <summary>Unique within the whole graph. Reported back in diagnostics and the source map.</summary>
        public string Id;

        /// <summary>The block kind, for example <c>set</c>, <c>if</c>, <c>add</c> or <c>call</c>.</summary>
        public string Block;

        /// <summary>The variable name for <c>get</c>/<c>set</c>/<c>change</c>/<c>for_each</c>, or the function name for <c>call</c>.</summary>
        public string Name;

        public List<BlockInput> Inputs = new List<BlockInput>();
        public List<BlockLink> Flow = new List<BlockLink>();

        public BlockNode() { }

        public BlockNode(string id, string block, string name = null)
        {
            Id = id;
            Block = block;
            Name = name;
        }

        /// <summary>Connects another block's output to an input pin. Name the output with <paramref name="fromPin"/> when that block has several.</summary>
        public BlockNode Connect(string pin, string fromNode, string fromPin = null)
        {
            Inputs.Add(new BlockInput(pin, fromNode, null, fromPin));
            return this;
        }

        /// <summary>Types a literal into an input pin.</summary>
        public BlockNode Literal(string pin, string literal)
        {
            Inputs.Add(new BlockInput(pin, null, literal));
            return this;
        }

        /// <summary>Links an execution pin (<c>next</c>, <c>then</c>, <c>else</c>, <c>body</c>) to another block.</summary>
        public BlockNode Then(string pin, string toNode)
        {
            Flow.Add(new BlockLink(pin, toNode));
            return this;
        }

        /// <summary>Links the <c>next</c> pin.</summary>
        public BlockNode Then(string toNode) => Then("next", toNode);
    }

    /// <summary>
    /// A function in the graph. Event handlers and user-made blocks are both functions: the function's name is the
    /// event name or the block's name, and a user-made block is used from other functions with a <c>call</c> block.
    /// </summary>
    [Serializable]
    public sealed class BlockFunction
    {
        public string Name;
        public List<string> Parameters = new List<string>();

        /// <summary>The id of the first block that runs, or empty for a function that does nothing.</summary>
        public string Start;

        public List<BlockNode> Nodes = new List<BlockNode>();

        public BlockFunction() { }

        public BlockFunction(string name, params string[] parameters)
        {
            Name = name;
            Parameters.AddRange(parameters);
        }

        public BlockFunction Add(BlockNode node)
        {
            Nodes.Add(node);
            return this;
        }
    }

    /// <summary>A global variable shared by every function in the graph.</summary>
    [Serializable]
    public sealed class BlockVariable
    {
        public string Name;

        /// <summary>The starting value, as a single Python literal.</summary>
        public string Initial;

        /// <summary>
        /// The declared type, such as <c>int</c>, <c>list[str]</c> or a host handle name. Empty means <c>any</c>. A
        /// number, bool or string variable with no <see cref="Initial"/> starts as 0, 0.0, False or "".
        /// </summary>
        public string Type;

        public BlockVariable() { }

        public BlockVariable(string name, string initial, string type = null)
        {
            Name = name;
            Initial = initial;
            Type = type;
        }
    }

    /// <summary>A whole block program: global variables plus functions. <see cref="BlockCompiler"/> turns it into Python source.</summary>
    [Serializable]
    public sealed class BlockGraph
    {
        /// <summary>The format version written by <see cref="ToJson"/>. A document with a newer version is refused.</summary>
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public List<BlockVariable> Variables = new List<BlockVariable>();
        public List<BlockFunction> Functions = new List<BlockFunction>();

        /// <summary>Positions, comment boxes, reroute points and groups for an editor. The compiler ignores it.</summary>
        public BlockEditorData Editor = new BlockEditorData();

        public BlockGraph AddVariable(string name, string initial, string type = null)
        {
            Variables.Add(new BlockVariable(name, initial, type));
            return this;
        }

        public BlockGraph AddFunction(BlockFunction function)
        {
            if (function == null) throw new ArgumentNullException(nameof(function));
            Functions.Add(function);
            return this;
        }

        /// <summary>Writes the graph as JSON. The field names match the public fields, so Unity's <c>JsonUtility</c> reads it too.</summary>
        public string ToJson(bool indent = true) => BlockJson.Write(this, indent);

        /// <summary>Reads a graph written by <see cref="ToJson"/> or by <c>JsonUtility</c>. Throws <see cref="BlockGraphFormatException"/> for a bad document.</summary>
        public static BlockGraph FromJson(string json) => BlockJson.Read(json);
    }
}
