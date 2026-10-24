using System.Collections.Generic;
using CSharpPython.Syntax;

namespace CSharpPython.Compilation
{
    /// <summary>
    /// Decides which names are local and which are global, before anything runs. It also reports structural
    /// errors (misplaced <c>return</c>/<c>break</c>, bad <c>global</c> declarations) with line numbers.
    /// </summary>
    internal sealed class Resolver
    {
        private readonly ResolvedModule _result;
        private Scope _scope;
        private HashSet<string> _used;
        private int _loopDepth;

        private Resolver(ModuleNode module)
        {
            _result = new ResolvedModule(module);
            _scope = _result.ModuleScope;
            _used = new HashSet<string>();
        }

        public static ResolvedModule Resolve(ModuleNode module)
        {
            var resolver = new Resolver(module);
            resolver.VisitBody(module.Body);
            resolver.Analyze(resolver._result.ModuleScope);
            return resolver._result;
        }

        private static PySyntaxError Error(string message, Node at) => new PySyntaxError(message, at.Line, at.Column);

        private void VisitBody(List<Stmt> body)
        {
            foreach (var s in body) VisitStmt(s);
        }

        private void VisitStmt(Stmt stmt)
        {
            switch (stmt)
            {
                case ExprStmt e:
                    VisitExpr(e.Value);
                    break;
                case AssignStmt a:
                    VisitExpr(a.Value);
                    foreach (var t in a.Targets) VisitTarget(t);
                    break;
                case AugAssignStmt a:
                    VisitExpr(a.Value);
                    VisitExpr(a.Target); // the old value is read...
                    VisitTarget(a.Target); // ...and then rebound
                    break;
                case IfStmt i:
                    VisitExpr(i.Test);
                    VisitBody(i.Body);
                    VisitBody(i.Else);
                    break;
                case WhileStmt w:
                    VisitExpr(w.Test);
                    _loopDepth++;
                    VisitBody(w.Body);
                    _loopDepth--;
                    break;
                case ForStmt f:
                    VisitExpr(f.Iterable);
                    VisitTarget(f.Target);
                    _loopDepth++;
                    VisitBody(f.Body);
                    _loopDepth--;
                    break;
                case DefStmt d:
                    VisitDef(d);
                    break;
                case ReturnStmt r:
                    if (_scope.IsModule) throw Error("'return' outside function", r);
                    if (r.Value != null) VisitExpr(r.Value);
                    break;
                case BreakStmt b:
                    if (_loopDepth == 0) throw Error("'break' outside loop", b);
                    break;
                case ContinueStmt c:
                    if (_loopDepth == 0) throw Error("'continue' not properly in loop", c);
                    break;
                case GlobalStmt g:
                    VisitGlobal(g);
                    break;
                case NonlocalStmt n:
                    VisitNonlocal(n);
                    break;
                case TryStmt t:
                    VisitBody(t.Body);
                    foreach (var h in t.Handlers)
                    {
                        if (h.Type != null) VisitExpr(h.Type);
                        if (h.Name != null) Bind(h.Name);
                        VisitBody(h.Body);
                    }
                    VisitBody(t.Else);
                    if (t.Finally != null) VisitBody(t.Finally);
                    break;
                case RaiseStmt r:
                    if (r.Value != null) VisitExpr(r.Value);
                    break;
                case ImportStmt i:
                    Bind(i.Alias ?? i.Module);
                    break;
                case ImportFromStmt i:
                    for (int k = 0; k < i.Names.Count; k++) Bind(i.Aliases[k] ?? i.Names[k]);
                    break;
                case PassStmt _:
                    break;
            }
        }

        private void VisitGlobal(GlobalStmt g)
        {
            if (_scope.IsModule) return; // 'global x' at module level is a no-op, as in Python
            foreach (string name in g.Names)
            {
                if (_scope.IsLocal(name))
                    throw Error("name '" + name + "' is assigned to before global declaration", g);
                if (_used.Contains(name))
                    throw Error("name '" + name + "' is used prior to global declaration", g);
                if (_scope.DeclaredNonlocals.Contains(name))
                    throw Error("name '" + name + "' is nonlocal and global", g);
                _scope.DeclaredGlobals.Add(name);
            }
        }

        private void VisitNonlocal(NonlocalStmt n)
        {
            if (_scope.IsModule) throw Error("nonlocal declaration not allowed at module level", n);
            foreach (string name in n.Names)
            {
                if (_scope.Function.Params.Exists(p => p.Name == name))
                    throw Error("name '" + name + "' is parameter and nonlocal", n);
                if (_scope.IsLocal(name))
                    throw Error("name '" + name + "' is assigned to before nonlocal declaration", n);
                if (_used.Contains(name))
                    throw Error("name '" + name + "' is used prior to nonlocal declaration", n);
                if (_scope.DeclaredGlobals.Contains(name))
                    throw Error("name '" + name + "' is nonlocal and global", n);
                _scope.DeclaredNonlocals.Add(name);
                if (!_scope.NonlocalSites.ContainsKey(name)) _scope.NonlocalSites[name] = n;
            }
        }

        private void VisitDef(DefStmt d)
        {
            VisitFunction(d);
            Bind(d.Name);
        }

        // Resolves a def or lambda. Defaults are evaluated in the enclosing scope, when the function is created.
        private void VisitFunction(DefStmt d)
        {
            foreach (var p in d.Params)
                if (p.Default != null) VisitExpr(p.Default);

            var scope = new Scope(d, _scope);
            foreach (var p in d.Params)
            {
                if (scope.IsLocal(p.Name))
                    throw new PySyntaxError("duplicate argument '" + p.Name + "' in function definition", p.Line, p.Column);
                scope.AddLocal(p.Name);
            }
            _result.Functions[d] = scope;

            var savedScope = _scope;
            var savedUsed = _used;
            int savedLoops = _loopDepth;
            _scope = scope;
            _used = new HashSet<string>();
            _loopDepth = 0;
            VisitBody(d.Body);
            _scope = savedScope;
            _used = savedUsed;
            _loopDepth = savedLoops;
        }

        // Works out, for every function, which of its locals inner functions share (cells) and which names it
        // takes from enclosing functions (free variables). Returns the free variables of the scope.
        private void Analyze(Scope scope)
        {
            var fromChildren = new HashSet<string>();
            foreach (var child in scope.Children)
            {
                Analyze(child);
                foreach (var name in child.FreeVars) fromChildren.Add(name);
            }

            var names = new List<string>(scope.Used);
            foreach (var child in scope.Children)
                foreach (var name in child.FreeVars)
                    if (!names.Contains(name)) names.Add(name);
            foreach (var name in scope.DeclaredNonlocals)
                if (!names.Contains(name)) names.Add(name);

            foreach (var name in names)
            {
                if (scope.IsModule || scope.DeclaredGlobals.Contains(name)) continue;
                if (scope.IsLocal(name))
                {
                    if (fromChildren.Contains(name)) scope.MakeCell(name);
                }
                else if (IsLocalOfEnclosing(scope, name)) scope.AddFree(name);
                else if (scope.DeclaredNonlocals.Contains(name))
                    throw Error("no binding for nonlocal '" + name + "' found", scope.NonlocalSites[name]);
            }
        }

        private static bool IsLocalOfEnclosing(Scope scope, string name)
        {
            for (var p = scope.Parent; p != null && !p.IsModule; p = p.Parent)
            {
                if (p.IsLocal(name)) return true;
                if (p.DeclaredGlobals.Contains(name)) return false;
            }
            return false;
        }

        // Records that a name is bound (assigned) in the current scope.
        private void Bind(string name)
        {
            if (_scope.IsModule || _scope.DeclaredGlobals.Contains(name) || _scope.DeclaredNonlocals.Contains(name)) return;
            _scope.AddLocal(name);
        }

        private void VisitTarget(Expr target)
        {
            switch (target)
            {
                case NameExpr n:
                    Bind(n.Name);
                    break;
                case AttributeExpr a:
                    VisitExpr(a.Target);
                    break;
                case SubscriptExpr s:
                    VisitExpr(s.Target);
                    VisitExpr(s.Index);
                    break;
                case TupleExpr t:
                    foreach (var item in t.Items) VisitTarget(item);
                    break;
                case ListExpr l:
                    foreach (var item in l.Items) VisitTarget(item);
                    break;
            }
        }

        private void VisitExpr(Expr expr)
        {
            switch (expr)
            {
                case null:
                case ConstantExpr _:
                    break;
                case NameExpr n:
                    _used.Add(n.Name);
                    _scope.Use(n.Name);
                    break;
                case LambdaExpr l:
                    VisitFunction(l.Function);
                    break;
                case UnaryExpr u:
                    VisitExpr(u.Operand);
                    break;
                case BinaryExpr b:
                    VisitExpr(b.Left);
                    VisitExpr(b.Right);
                    break;
                case BoolOpExpr b:
                    VisitExpr(b.Left);
                    VisitExpr(b.Right);
                    break;
                case CompareExpr c:
                    VisitExpr(c.First);
                    foreach (var r in c.Rest) VisitExpr(r);
                    break;
                case CallExpr c:
                    VisitExpr(c.Func);
                    foreach (var a in c.Args) VisitExpr(a);
                    foreach (var v in c.KeywordValues) VisitExpr(v);
                    break;
                case AttributeExpr a:
                    VisitExpr(a.Target);
                    break;
                case SubscriptExpr s:
                    VisitExpr(s.Target);
                    VisitExpr(s.Index);
                    break;
                case SliceExpr s:
                    VisitExpr(s.Lower);
                    VisitExpr(s.Upper);
                    VisitExpr(s.Step);
                    break;
                case ListExpr l:
                    foreach (var item in l.Items) VisitExpr(item);
                    break;
                case TupleExpr t:
                    foreach (var item in t.Items) VisitExpr(item);
                    break;
                case DictExpr d:
                    foreach (var k in d.Keys) VisitExpr(k);
                    foreach (var v in d.Values) VisitExpr(v);
                    break;
                case IfExpr i:
                    VisitExpr(i.Test);
                    VisitExpr(i.Then);
                    VisitExpr(i.Else);
                    break;
                case FStringExpr f:
                    foreach (var part in f.Parts) VisitExpr(part.Value);
                    break;
            }
        }
    }
}
