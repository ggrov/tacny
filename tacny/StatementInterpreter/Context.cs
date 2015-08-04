using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    public class Context
    {
        public readonly MemberDecl md;
        public readonly UpdateStmt tac_call;

        public Context(MemberDecl md, UpdateStmt tac_call)
        {
            Contract.Requires(md != null && tac_call != null);
            this.md = md;
            this.tac_call = tac_call;
        }

    }

    /// <summary>
    /// Local context for the tactic currently being resolved
    /// </summary>
    public class LocalContext : Context
    {
        public readonly Tactic tac = null;  // The called tactic
        public List<Statement> tac_body = new List<Statement>(); // body of the currently worked tactic
        public Dictionary<Dafny.IVariable, object> local_variables = new Dictionary<Dafny.IVariable, object>();
        public Dictionary<Statement, Statement> updated_statements = new Dictionary<Statement, Statement>();
        public List<Statement> resolved = new List<Statement>();

        public LocalContext(MemberDecl md, Tactic tac, UpdateStmt tac_call) : base(md, tac_call)
        {
            this.tac = tac;
            this.tac_body = new List<Statement>(tac.Body.Body.ToArray());
        }

        public LocalContext(MemberDecl md, Tactic tac, UpdateStmt tac_call, List<Statement> tac_body, Dictionary<Dafny.IVariable, object> local_variables,
            Dictionary<Statement, Statement> updated_statements, List<Statement> resolved) : base(md, tac_call)
        {
            this.tac_body = new List<Statement>(tac_body.ToArray());

            List<IVariable> lv_keys = new List<IVariable>(local_variables.Keys);
            List<object> lv_values = new List<object>(local_variables.Values);
            this.local_variables = lv_keys.ToDictionary(x => x, x => lv_values[lv_keys.IndexOf(x)]);

            List<Statement> us_keys = new List<Statement>(updated_statements.Keys);
            List<Statement> us_values = new List<Statement>(updated_statements.Values);
            this.updated_statements = us_keys.ToDictionary(x => x, x => us_values[us_keys.IndexOf(x)]);

            this.resolved = new List<Statement>(resolved.ToArray());
        }

        public LocalContext Copy()
        {
            return new LocalContext(md, tac, tac_call, tac_body, local_variables, updated_statements, resolved);
        }
    }

    public class GlobalContext : Context
    {
        protected readonly Dictionary<string, DatatypeDecl> global_variables = new Dictionary<string, DatatypeDecl>();
        public Program program;


        public GlobalContext(MemberDecl md, UpdateStmt tac_call, Program program) : base(md, tac_call)
        {
            this.program = program;
            foreach (DatatypeDecl tld in program.globals)
                this.global_variables.Add(tld.Name, tld);
        }

        public bool ContainsGlobalKey(string name)
        {
            return global_variables.ContainsKey(name);
        }

        public DatatypeDecl GetGlobal(string name)
        {
            return global_variables[name];
        }
    }
}
