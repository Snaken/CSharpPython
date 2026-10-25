using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSharpPython.Syntax;

namespace CSharpPython.Tests.Syntax
{
    /// <summary>Renders syntax trees as compact S-expressions so tests can assert on structure in one line.</summary>
    public static class AstPrinter
    {
        public static string Expr(Expr e)
        {
            switch (e)
            {
                case null: return "_";
                case ConstantExpr c: return Constant(c.Value);
                case NameExpr n: return n.Name;
                case UnaryExpr u: return "(" + (u.Op == UnaryOp.Negate ? "neg" : u.Op == UnaryOp.Plus ? "pos" : "not") + " " + Expr(u.Operand) + ")";
                case BinaryExpr b: return "(" + Op(b.Op) + " " + Expr(b.Left) + " " + Expr(b.Right) + ")";
                case BoolOpExpr b: return "(" + (b.IsAnd ? "and" : "or") + " " + Expr(b.Left) + " " + Expr(b.Right) + ")";
                case CompareExpr c:
                {
                    var parts = new List<string> { Expr(c.First) };
                    for (int i = 0; i < c.Ops.Count; i++) { parts.Add(CmpOp(c.Ops[i])); parts.Add(Expr(c.Rest[i])); }
                    return "(cmp " + string.Join(" ", parts) + ")";
                }
                case CallExpr c:
                {
                    var parts = new List<string> { "call", Expr(c.Func) };
                    parts.AddRange(c.Args.Select(Expr));
                    for (int i = 0; i < c.KeywordNames.Count; i++) parts.Add(c.KeywordNames[i] + "=" + Expr(c.KeywordValues[i]));
                    return "(" + string.Join(" ", parts) + ")";
                }
                case AttributeExpr a: return "(. " + Expr(a.Target) + " " + a.Name + ")";
                case SubscriptExpr s: return "(idx " + Expr(s.Target) + " " + Expr(s.Index) + ")";
                case SliceExpr s: return "(slice " + Expr(s.Lower) + " " + Expr(s.Upper) + " " + Expr(s.Step) + ")";
                case StarExpr st: return "(* " + Expr(st.Value) + ")";
                case ListExpr l: return "[" + string.Join(" ", l.Items.Select(Expr)) + "]";
                case TupleExpr t: return "(tuple" + string.Concat(t.Items.Select(i => " " + Expr(i))) + ")";
                case DictExpr d:
                    return "{" + string.Join(" ", d.Keys.Select((k, i) => (k == null ? "**" : Expr(k) + ":") + Expr(d.Values[i]))) + "}";
                case IfExpr i: return "(ifexp " + Expr(i.Test) + " " + Expr(i.Then) + " " + Expr(i.Else) + ")";
                case FStringExpr f: return "(f" + string.Concat(f.Parts.Select(FPart)) + ")";
                default: return "?" + e.GetType().Name;
            }
        }

        private static string FPart(FStringPart p)
        {
            if (p.Value == null) return " '" + p.Literal + "'";
            string s = " (val " + Expr(p.Value);
            if (p.Conversion != '\0') s += " !" + p.Conversion;
            if (p.FormatSpec != null) s += " :" + p.FormatSpec;
            return s + ")";
        }

        private static string Constant(object v)
        {
            switch (v)
            {
                case null: return "None";
                case bool b: return b ? "True" : "False";
                case long l: return l.ToString(CultureInfo.InvariantCulture);
                case double d: return d.ToString("R", CultureInfo.InvariantCulture) + (d == System.Math.Floor(d) && !double.IsInfinity(d) ? ".0" : "");
                case string s: return "'" + s + "'";
                default: return "?" + v;
            }
        }

        private static string Op(BinaryOp op)
        {
            switch (op)
            {
                case BinaryOp.Add: return "+";
                case BinaryOp.Subtract: return "-";
                case BinaryOp.Multiply: return "*";
                case BinaryOp.Divide: return "/";
                case BinaryOp.FloorDivide: return "//";
                case BinaryOp.Modulo: return "%";
                default: return "**";
            }
        }

        private static string CmpOp(CompareOp op)
        {
            switch (op)
            {
                case CompareOp.Eq: return "==";
                case CompareOp.NotEq: return "!=";
                case CompareOp.Lt: return "<";
                case CompareOp.Le: return "<=";
                case CompareOp.Gt: return ">";
                case CompareOp.Ge: return ">=";
                case CompareOp.Is: return "is";
                case CompareOp.IsNot: return "is-not";
                case CompareOp.In: return "in";
                default: return "not-in";
            }
        }

        public static string Body(List<Stmt> body) => "[" + string.Join("; ", body.Select(Stmt)) + "]";

        public static string Stmt(Stmt s)
        {
            switch (s)
            {
                case ExprStmt e: return "(expr " + Expr(e.Value) + ")";
                case AssignStmt a: return "(assign " + string.Join(" ", a.Targets.Select(Expr)) + " = " + Expr(a.Value) + ")";
                case AugAssignStmt a: return "(aug " + Op(a.Op) + "= " + Expr(a.Target) + " " + Expr(a.Value) + ")";
                case IfStmt i: return "(if " + Expr(i.Test) + " " + Body(i.Body) + (i.Else.Count > 0 ? " " + Body(i.Else) : "") + ")";
                case WhileStmt w: return "(while " + Expr(w.Test) + " " + Body(w.Body) + ")";
                case ForStmt f: return "(for " + Expr(f.Target) + " " + Expr(f.Iterable) + " " + Body(f.Body) + ")";
                case DefStmt d:
                    return "(def " + d.Name + " (" + string.Join(" ", d.Params.Select(p => p.Name + (p.Default != null ? "=" + Expr(p.Default) : ""))) + ") " + Body(d.Body) + ")";
                case ReturnStmt r: return r.Value == null ? "(return)" : "(return " + Expr(r.Value) + ")";
                case BreakStmt _: return "break";
                case ContinueStmt _: return "continue";
                case PassStmt _: return "pass";
                case GlobalStmt g: return "(global " + string.Join(" ", g.Names) + ")";
                case TryStmt t:
                    return "(try " + Body(t.Body) + string.Concat(t.Handlers.Select(h =>
                        " (except " + (h.Type == null ? "_" : Expr(h.Type)) + (h.Name != null ? " as " + h.Name : "") + " " + Body(h.Body) + ")")) + ")";
                case RaiseStmt r: return r.Value == null ? "(raise)" : "(raise " + Expr(r.Value) + ")";
                case ImportStmt i: return "(import " + i.Module + (i.Alias != null ? " as " + i.Alias : "") + ")";
                case ImportFromStmt i:
                    return "(from " + i.Module + " import " + string.Join(", ", i.Names.Select((n, k) => n + (i.Aliases[k] != null ? " as " + i.Aliases[k] : ""))) + ")";
                default: return "?" + s.GetType().Name;
            }
        }

        public static string Module(string source) => string.Join("\n", Parser.ParseModule(source).Body.Select(Stmt));
    }
}
