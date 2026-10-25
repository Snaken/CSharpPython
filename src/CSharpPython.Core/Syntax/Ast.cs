using System.Collections.Generic;

namespace CSharpPython.Syntax
{
    /// <summary>Base class of every syntax tree node. Positions are one-based.</summary>
    public abstract class Node
    {
        public int Line;
        public int Column;
    }

    // ---- Expressions --------------------------------------------------------------------------------------------

    public abstract class Expr : Node { }

    /// <summary>A literal. <see cref="Value"/> is null (None), bool, long, double or string.</summary>
    public sealed class ConstantExpr : Expr
    {
        public object Value;
    }

    public sealed class NameExpr : Expr
    {
        public string Name;
    }

    public enum UnaryOp { Negate, Plus, Not }

    public sealed class UnaryExpr : Expr
    {
        public UnaryOp Op;
        public Expr Operand;
    }

    public enum BinaryOp { Add, Subtract, Multiply, Divide, FloorDivide, Modulo, Power }

    public sealed class BinaryExpr : Expr
    {
        public BinaryOp Op;
        public Expr Left, Right;
    }

    /// <summary>Short-circuiting <c>and</c> / <c>or</c>.</summary>
    public sealed class BoolOpExpr : Expr
    {
        public bool IsAnd;
        public Expr Left, Right;
    }

    public enum CompareOp { Eq, NotEq, Lt, Le, Gt, Ge, Is, IsNot, In, NotIn }

    /// <summary>A possibly chained comparison: <c>a &lt; b &lt;= c</c>.</summary>
    public sealed class CompareExpr : Expr
    {
        public Expr First;
        public List<CompareOp> Ops = new List<CompareOp>();
        public List<Expr> Rest = new List<Expr>();
    }

    public sealed class CallExpr : Expr
    {
        public Expr Func;
        public List<Expr> Args = new List<Expr>();
        /// <summary>
        /// Names of keyword arguments, parallel to <see cref="KeywordValues"/>. A null name marks a <c>**mapping</c>
        /// argument. <see cref="Args"/> may contain <see cref="StarExpr"/> items for <c>*iterable</c> arguments.
        /// </summary>
        public List<string> KeywordNames = new List<string>();
        public List<Expr> KeywordValues = new List<Expr>();
    }

    /// <summary>A <c>*iterable</c> argument in a call.</summary>
    public sealed class StarExpr : Expr
    {
        public Expr Value;
    }

    public sealed class AttributeExpr : Expr
    {
        public Expr Target;
        public string Name;
    }

    public sealed class SubscriptExpr : Expr
    {
        public Expr Target;
        public Expr Index;
    }

    /// <summary>A slice such as <c>a[1:3]</c>; any part may be null.</summary>
    public sealed class SliceExpr : Expr
    {
        public Expr Lower, Upper, Step;
    }

    public sealed class ListExpr : Expr
    {
        public List<Expr> Items = new List<Expr>();
    }

    public sealed class TupleExpr : Expr
    {
        public List<Expr> Items = new List<Expr>();
    }

    public sealed class SetExpr : Expr
    {
        public List<Expr> Items = new List<Expr>();
    }

    public sealed class DictExpr : Expr
    {
        public List<Expr> Keys = new List<Expr>();
        public List<Expr> Values = new List<Expr>();
    }

    /// <summary>The conditional expression <c>a if cond else b</c>.</summary>
    public sealed class IfExpr : Expr
    {
        public Expr Test, Then, Else;
    }

    /// <summary>A <c>lambda</c>: an anonymous function whose body is one expression, held as a <c>return</c> in <see cref="Function"/>.</summary>
    public sealed class LambdaExpr : Expr
    {
        public DefStmt Function;
    }

    /// <summary>One piece of an f-string: literal text or a formatted expression.</summary>
    public sealed class FStringPart
    {
        public string Literal;
        public Expr Value;
        /// <summary>'\0' for none, otherwise 's', 'r' or 'a'.</summary>
        public char Conversion;
        public string FormatSpec;
    }

    public sealed class FStringExpr : Expr
    {
        public List<FStringPart> Parts = new List<FStringPart>();
    }

    // ---- Statements ---------------------------------------------------------------------------------------------

    public abstract class Stmt : Node { }

    public sealed class ExprStmt : Stmt
    {
        public Expr Value;
    }

    /// <summary>Plain assignment, including chained (<c>a = b = 1</c>) and unpacking (<c>a, b = x</c>).</summary>
    public sealed class AssignStmt : Stmt
    {
        public List<Expr> Targets = new List<Expr>();
        public Expr Value;
    }

    public sealed class AugAssignStmt : Stmt
    {
        public BinaryOp Op;
        public Expr Target;
        public Expr Value;
    }

    public sealed class IfStmt : Stmt
    {
        public Expr Test;
        public List<Stmt> Body = new List<Stmt>();
        /// <summary>Statements of the else branch. An <c>elif</c> is a single nested <see cref="IfStmt"/>.</summary>
        public List<Stmt> Else = new List<Stmt>();
    }

    public sealed class WhileStmt : Stmt
    {
        public Expr Test;
        public List<Stmt> Body = new List<Stmt>();
    }

    public sealed class ForStmt : Stmt
    {
        public Expr Target;
        public Expr Iterable;
        public List<Stmt> Body = new List<Stmt>();
    }

    public sealed class Param
    {
        public string Name;
        public Expr Default;
        public int Line, Column;
    }

    public sealed class DefStmt : Stmt
    {
        public string Name;
        public List<Param> Params = new List<Param>();

        /// <summary>Parameters after <c>*args</c> or a bare <c>*</c>; they can only be given by keyword.</summary>
        public List<Param> KeywordOnly = new List<Param>();

        /// <summary>The <c>*args</c> and <c>**kwargs</c> parameters, or null.</summary>
        public Param VarArgs, VarKeywords;

        /// <summary>Every parameter in local-slot order: positional, keyword-only, *args, **kwargs.</summary>
        public IEnumerable<Param> AllParams()
        {
            foreach (var p in Params) yield return p;
            foreach (var p in KeywordOnly) yield return p;
            if (VarArgs != null) yield return VarArgs;
            if (VarKeywords != null) yield return VarKeywords;
        }

        public List<Stmt> Body = new List<Stmt>();
    }

    /// <summary>
    /// A class definition. The body is kept as a parameterless <see cref="DefStmt"/> because it runs once, like a
    /// function call, and its local variables become the class attributes.
    /// </summary>
    public sealed class ClassStmt : Stmt
    {
        public string Name;
        public List<Expr> Bases = new List<Expr>();
        public DefStmt Body;
    }

    public sealed class ReturnStmt : Stmt
    {
        public Expr Value;
    }

    public sealed class BreakStmt : Stmt { }

    public sealed class ContinueStmt : Stmt { }

    public sealed class PassStmt : Stmt { }

    public sealed class GlobalStmt : Stmt
    {
        public List<string> Names = new List<string>();
    }

    public sealed class NonlocalStmt : Stmt
    {
        public List<string> Names = new List<string>();
    }

    public sealed class ExceptHandler
    {
        /// <summary>The exception type expression, or null for a bare <c>except:</c>.</summary>
        public Expr Type;
        public string Name;
        public List<Stmt> Body = new List<Stmt>();
        public int Line, Column;
    }

    public sealed class TryStmt : Stmt
    {
        public List<Stmt> Body = new List<Stmt>();
        public List<ExceptHandler> Handlers = new List<ExceptHandler>();

        /// <summary>The <c>else</c> block, run when the body raised nothing. Empty when absent.</summary>
        public List<Stmt> Else = new List<Stmt>();

        /// <summary>The <c>finally</c> block, or null when absent.</summary>
        public List<Stmt> Finally;
    }

    public sealed class RaiseStmt : Stmt
    {
        /// <summary>The exception to raise, or null for a bare <c>raise</c> that re-raises.</summary>
        public Expr Value;
    }

    public sealed class ImportStmt : Stmt
    {
        public string Module;
        public string Alias;
    }

    public sealed class ImportFromStmt : Stmt
    {
        public string Module;
        public List<string> Names = new List<string>();
        public List<string> Aliases = new List<string>();
    }

    /// <summary>The parsed form of a whole script.</summary>
    public sealed class ModuleNode : Node
    {
        public List<Stmt> Body = new List<Stmt>();
    }
}
