using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;
using Bpl = Microsoft.Boogie;
using System.Diagnostics.Contracts;


namespace Tacny
{

    public class ResolutionErrorReporter
    {
        public int ErrorCount = 0;

        public void Error(IToken tok, string msg, params object[] args)
        {
            Contract.Requires(tok != null);
            Contract.Requires(msg != null);
            ConsoleColor col = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine("{0}({1},{2}): Error: {3}",
                DafnyOptions.Clo.UseBaseNameForFileName ? System.IO.Path.GetFileName(tok.filename) : tok.filename, tok.line, tok.col - 1,
                string.Format(msg));
            Console.ForegroundColor = col;
            ErrorCount++;
        }

        public void Error(Statement s, string msg, params object[] args)
        {
            Contract.Requires(s != null);
            Contract.Requires(msg != null);
            Error(s.Tok, msg, args);
        }

        public static void Warning(IToken tok, string msg)
        {
            Contract.Requires(msg != null);
            Contract.Requires(tok != null);
            ConsoleColor col = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("{0}({1},{2}): Warning: {3}",
                DafnyOptions.Clo.UseBaseNameForFileName ? System.IO.Path.GetFileName(tok.filename) : tok.filename, tok.line, tok.col - 1,
                string.Format(msg));
            Console.ForegroundColor = col;
        }

        public static void Warning(Statement s, string msg)
        {
            Contract.Requires(msg != null);
            Contract.Requires(s != null);
            Warning(s.Tok, msg);
        }

        public static void Message(string msg, IToken tok = null)
        {
            Contract.Requires(msg != null);
            ConsoleColor col = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("DEBUG: {0}",
                string.Format(msg));
            Console.ForegroundColor = col;
        }
    }

    public class Interpreter : ResolutionErrorReporter
    {
        enum Atomic
        {
            UNDEFINED = 0,
            ASSERT,
            ADD_INVAR,
            CREATE_INVAR,
            REPLACE_SINGLETON,
            EXTRACT_GUARD,
            REPLACE_OP
        };

        private Dictionary<string, MemberDecl> tactics = new Dictionary<string, MemberDecl>();
        private Dafny.Program program;
        private SolutionTree solution_tree = null;
        private IList<string> fileNames;
        private string programId;

        public Interpreter(Dafny.Program program, IList<string> fileNames, string programId)
        {
            Contract.Requires(program != null);
            this.program = program;
            this.fileNames = fileNames;
            this.programId = programId;
        }

        public bool HasTactics()
        {
            foreach (TopLevelDecl tld in program.DefaultModuleDef.TopLevelDecls)
            {
                if (tld is Dafny.ClassDecl)
                {
                    Dafny.ClassDecl tmp = (Dafny.ClassDecl)tld;
                    foreach (Dafny.MemberDecl member in tmp.Members)
                    {
                        if (member is Dafny.Tactic)
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Check if MemberDecl contains calls to tactics
        /// </summary>
        /// <returns></returns>
        private bool HasTactics(MemberDecl md)
        {
            if (md is Method)
            {
                Method m = (Method)md;
                if (m.Body == null)
                    return false;
                foreach (Statement s in m.Body.SubStatements)
                {
                    // go deeper and check if it's a CallStmt
                    if (s is UpdateStmt)
                    {
                        UpdateStmt us = (UpdateStmt)s;
                        ExprRhs er = us.Rhss[0] as ExprRhs;
                        if (er == null)
                            return false;
                        ApplySuffix asx = er.Expr as ApplySuffix;
                        if (asx == null)
                            return false;
                        string name = asx.Lhs.tok.val;
                        string sPatten = ".+_tactic$";
                        bool tmp = Regex.IsMatch(name, sPatten);
                        return tmp;

                    }
                }
            }
            return false;
        }

        public string ResolveProgram(ref Dafny.Program prg)
        {
            ClassDecl curDecl;
            string err;

            for (int i = 0; i < prg.DefaultModuleDef.TopLevelDecls.Count; i++)
            {
                TopLevelDecl d = prg.DefaultModuleDef.TopLevelDecls[i];
                if (d is ClassDecl)
                {
                    // scan each member for tactic calls and resolve if found
                    curDecl = (ClassDecl)d;
                    for (int j = 0; j < curDecl.Members.Count; j++)
                    {
                        MemberDecl md = curDecl.Members[j];
                        if (md is Tactic)
                        {
                            tactics.Add(md.Name, (Tactic)md);
                        }
                        else
                        {
                            err = ScanMemberBody(md);
                            if (err != null)
                                return err;
                        }
                    }
                }
            }

            err = VerifySolutionTree(solution_tree, ref prg);
            return err;
        }

        /// <summary>
        /// Traverses the tree. Generates, resolves and verifies each leaf node until a 
        /// valid proof is found
        /// </summary>
        /// <param name="solution_tree"></param>
        /// <returns></returns>
        private string VerifySolutionTree(SolutionTree sol_tree, ref Dafny.Program result)
        {
            string err = null;
            if (sol_tree.isLeaf())
            {
                if (!sol_tree.isFinal)
                    return "VerifdySolutionTree: Received non final leaf";
                Dafny.Program prog = GenerateProgram(sol_tree);
                err = Tacny.Main.ResolveProgram(prog);
                if (err != null)
                    return err;
                Bpl.Program boogieProgram;
                PipelineOutcome po;
                Bpl.PipelineStatistics stats;

                Tacny.Main.Translate(prog, fileNames, programId, out boogieProgram);
                po = Tacny.Main.BoogiePipeline(boogieProgram, prog, fileNames, programId, out stats);
                if (stats.ErrorCount == 0)
                {
                    result = prog;
                    return "done";
                }
                // do something with resolution results'in the future
            }
            else
            {
                foreach (SolutionTree child in sol_tree.children)
                {
                    err = VerifySolutionTree(child, ref result);
                    if (err == "done")
                        return null;
                }
            }
            return "VerifySolution: Unable to verifyt the generated soution";
        }

        private Dafny.Program GenerateProgram(SolutionTree st)
        {
            List<Dafny.Program> prog_list = new List<Dafny.Program>();
            Action ac = st.state;
            Method m = (Method)ac.md;
            UpdateStmt tac_call = ac.tac_call;
            List<Statement> body = m.Body.Body;
            body = InsertSolution(body, tac_call, ac.resolved);

            m = new Method(m.tok, m.Name, m.HasStaticKeyword, m.IsGhost,
                m.TypeArgs, m.Ins, m.Outs, m.Req, m.Mod, m.Ens, m.Decreases,
                new BlockStmt(m.Body.Tok, m.Body.EndTok, body), m.Attributes, m.SignatureEllipsis);
            Dafny.Program prog;
            Tacny.Main.ParseCheck(fileNames, programId, out prog);
            ClassDecl curDecl;

            for (int i = 0; i < prog.DefaultModuleDef.TopLevelDecls.Count; i++)
            {
                TopLevelDecl d = prog.DefaultModuleDef.TopLevelDecls[i];
                if (d is ClassDecl)
                {
                    // scan each member for tactic calls and resolve if found
                    curDecl = (ClassDecl)d;
                    for (int j = 0; j < curDecl.Members.Count; j++)
                    {
                        MemberDecl md = curDecl.Members[j];
                        if (md is Method)
                        {
                            Method old_m = (Method)md;
                            if (old_m.Name == m.Name)
                            {
                                curDecl.Members[j] = m;
                            }
                        }
                    }
                    prog.DefaultModuleDef.TopLevelDecls[i] = RemoveTactics(curDecl);
                }
            }

            return prog;
        }

        private List<Statement> InsertSolution(List<Statement> body, UpdateStmt tac_call, List<Statement> solution)
        {
            WhileStmt ws = null;
            BlockStmt bs = null;
            int index = body.IndexOf(tac_call);
            List<Statement> newBody = new List<Statement>();
            if (index == 0)
            {
                newBody = body;
                newBody.InsertRange(0, solution);
                return newBody;
            }

            // check where from tac_call has been made
            int i = index+1;
            while (i < body.Count)
            {
                Statement stmt = body[i];
                // if we found a block statement check behind to find the asociated while statement
                if (stmt is BlockStmt)
                {
                    int j = index;
                    while (j > 0)
                    {
                        Statement stmt_2 = body[j];
                        if (stmt_2 is WhileStmt)
                        {
                            ws = (WhileStmt)stmt_2;
                            break;
                        }
                        else if (!(stmt_2 is UpdateStmt))
                        {
                            return null;
                        }

                        j--;
                    }
                    bs = (BlockStmt)stmt;
                    break;
                }
                else if (!(stmt is UpdateStmt))
                {
                    return null;
                }

                i++;
            }
            //// tactic called from a while statement
            if (ws != null && bs != null)
            {
                Statement[] tmp = body.ToArray();
                int l_bound = body.IndexOf(ws);
                int u_boud = body.IndexOf(bs);
                // tactic called in a while statement should 
                //  return onlyt a single solution item which is a WhileStmt
                WhileStmt mod_ws = (WhileStmt)solution[0];
                mod_ws = new WhileStmt(mod_ws.Tok, mod_ws.EndTok, mod_ws.Guard, mod_ws.Invariants,
                    mod_ws.Decreases, mod_ws.Mod, bs);
                tmp[l_bound] = mod_ws;
                l_bound++;
                

                // for now remove everything between while stmt and block stmt
                while (l_bound  <= u_boud)
                {
                    tmp[l_bound] = null;
                    l_bound++;
                }

                foreach (Statement st in tmp)
                {
                    if (st != null)
                        newBody.Add(st);
                }
            }
            else
            {
                newBody = body;
                newBody.InsertRange(index, solution);
            }

            return newBody;
        }

        private TopLevelDecl RemoveTactics(ClassDecl cd)
        {
            List<MemberDecl> mdl = new List<MemberDecl>();
            foreach (MemberDecl md in cd.Members)
            {
                if (!(md is Tactic))
                    mdl.Add(md);

            }
            return new ClassDecl(cd.tok, cd.Name, cd.Module, cd.TypeArgs, mdl, cd.Attributes, cd.TraitsTyp);
        }

        // will probably require some handling to unresolvable tactics
        private string ScanMemberBody(MemberDecl md)
        {
            Method m = md as Method;
            if (m == null)
                return null;

            List<Statement> newBody = new List<Statement>();
            //extract body from tactics 
            foreach (Statement st in m.Body.Body)
            {
                if (st is UpdateStmt)
                {
                    UpdateStmt us = (UpdateStmt)st;
                    ExprRhs er = us.Rhss[0] as ExprRhs;
                    if (er == null)
                        return "change me error 1"; // TODO
                    ApplySuffix asx = er.Expr as ApplySuffix;
                    if (asx == null)
                        return "change me error 2"; // TODO
                    string name = asx.Lhs.tok.val;
                    string sPatten = ".+_tactic$";

                    if (Regex.IsMatch(name, sPatten))
                    {
                        Tactic tac;
                        if (!tactics.ContainsKey(name))
                            return "Error during resolution; tactic " + name + " not defined";
                        tac = (Tactic)tactics[name];
                        string err = ResolveTacticBody(tac, st as UpdateStmt, md); // generate a solution tree
                        if (err != null)
                            return err;

                    }
                }
            }

            return null;
        }

        private List<Statement> CleanBody(List<Statement> oldBody)
        {
            List<Statement> newBody = new List<Statement>();
            for (int i = 0; i < oldBody.Count; i++)
            {
                Statement s = oldBody[i];
                if (s is WhileStmt)
                {
                    if (oldBody[i + 1] is BlockStmt)
                    {
                        WhileStmt ws = (WhileStmt)s;

                        ws = new WhileStmt(ws.Tok, ws.EndTok, ws.Guard, ws.Invariants, ws.Decreases, ws.Mod, (BlockStmt)oldBody[i + 1]);
                        oldBody[i] = ws;
                        oldBody[i + 1] = null;
                    }
                    else
                    {
                        oldBody[i] = null;
                    }
                }
            }

            foreach (Statement s in oldBody)
                if (s != null)
                    newBody.Add(s);

            return newBody;
        }

        /// <summary>
        /// Generates a solution tree from the tactics body
        /// </summary>
        /// <param name="tac"></param>
        /// <param name="tac_call"></param>
        /// <param name="md"></param>
        /// <returns>null on success otherwise an error message is returned</returns>
        private string ResolveTacticBody(Tactic tac, UpdateStmt tac_call, MemberDecl md)
        {
            Contract.Requires(tac != null);
            Contract.Requires(tac_call != null);
            Contract.Requires(md != null);
            Action action;
            if (solution_tree != null)
            {
                solution_tree = solution_tree.GetLeftMost();
                action = solution_tree.state.Update(md, tac, tac_call);
            }
            else
            {
                action = new Action(md, tac, tac_call);
                solution_tree = new SolutionTree(action);
            }
            string err = null;

            foreach (Statement st in tac.Body.Body)
            {
                err = CallAction(st, action, ref solution_tree);
                if (err != null)
                    Error(st, err, null);
                solution_tree = solution_tree.GetLeftMost();
                action = solution_tree.state;
            }

            // fully resolve the left most branch
            err = action.Finalize(ref solution_tree);
            if (err != null)
            {
                Error(tac_call, err);
                err = null;
            }

            // time to backtrack ^.^ 
            solution_tree = solution_tree.root;
            err = FinalizeTree(solution_tree);
            solution_tree.PrintTree("-");
            return null;
        }

        private static string FinalizeTree(SolutionTree solution_tree)
        {
            string err;
            if (solution_tree.isFinal)
            {
                return null;
            }
            else if (solution_tree.isLeaf())
            {
                // resolve the node
                Action ac = solution_tree.state;
                int index = ac.tac.Body.Body.IndexOf(solution_tree.last_resolved);
                if (index < 0)
                    return "Error occured during tree finalization, index of last resolved statement is out of bound";

                for (int i = index + 1; i < ac.tac.Body.Body.Count; i++)
                {
                    
                    CallAction(ac.tac.Body.Body[i], ac, ref solution_tree);
                    solution_tree = solution_tree.GetLeftMostUndersolved();
                    ac = solution_tree.state;
                }
                // finalize only leaf nodes
                if (solution_tree.isLeaf())
                    return ac.Finalize(ref solution_tree);
                return null;
            }
            else
            {
                for (int i = 0; i < solution_tree.children.Count; i++)
                {
                    err = FinalizeTree(solution_tree.children[i]);
                    if (err != null)
                        return err;
                }
                solution_tree.isFinal = true;
            }

            return null;
        }

        private static string CallAction(Statement st, Action action,
            ref SolutionTree solution_tree)
        {
            string err;
            switch (GetType(st))
            {
                case Atomic.CREATE_INVAR:   // Create invariants from preconditions
                    err = action.CreateInvar(st, ref solution_tree);
                    break;
                case Atomic.ADD_INVAR:      // Add invariants to dafny program
                    err = action.AddInvar(st, ref solution_tree);
                    break;
                case Atomic.REPLACE_SINGLETON:
                    err = action.ReplaceSingleton(st, ref solution_tree);
                    break;
                case Atomic.EXTRACT_GUARD:
                    err = action.ExtractGuard(st, ref solution_tree);
                    break;
                case Atomic.REPLACE_OP:
                    err = action.ReplaceOperator(st, ref solution_tree);
                    break;
                default:
                    throw new tcce.UnreachableException();
            }
            return err;
        }


        private static Atomic GetType(Statement s)
        {
            Contract.Requires(s != null);
            ExprRhs er;
            UpdateStmt us;
            if (s is UpdateStmt)
            {
                us = s as UpdateStmt;

            }
            else if (s is VarDeclStmt)
            {
                VarDeclStmt vds = s as VarDeclStmt;
                us = vds.Update as UpdateStmt;
            }
            else
            {
                return Atomic.UNDEFINED;
            }

            er = (ExprRhs)us.Rhss[0];
            ApplySuffix ass = er.Expr as ApplySuffix;

            switch (ass.Lhs.tok.val)
            {
                case "add_assert":
                    return Atomic.ASSERT;
                case "add_invariant":
                    return Atomic.ADD_INVAR;
                case "create_invariant":
                    return Atomic.CREATE_INVAR;
                case "extract_guard":
                    return Atomic.EXTRACT_GUARD;
                case "replace_operator":
                    return Atomic.REPLACE_OP;
                case "replace_singleton":
                    return Atomic.REPLACE_SINGLETON;
                default:
                    return Atomic.UNDEFINED;
            }
        }
    }
}
