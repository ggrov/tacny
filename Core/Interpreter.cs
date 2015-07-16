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

        private Dictionary<string, Tactic> tactics = new Dictionary<string, Tactic>();
        private List<MemberDecl> members = new List<MemberDecl>();
        private List<TopLevelDecl> globals = new List<TopLevelDecl>();
        private Program tacnyProgram;

        private SolutionList solution_list = null;

        public Interpreter(Program tacnyProgram)
        {
            Contract.Requires(tacnyProgram != null);
            this.tacnyProgram = tacnyProgram;
            this.solution_list = new SolutionList();
        }

        public bool HasTactics()
        {
            foreach (TopLevelDecl tld in tacnyProgram.program.DefaultModuleDef.TopLevelDecls)
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

        public string ResolveProgram()
        {
            ClassDecl curDecl;
            string err = null;
            Dafny.Program prg = tacnyProgram.program;

            for (int i = 0; i < prg.DefaultModuleDef.TopLevelDecls.Count; i++)
            {
                TopLevelDecl d = prg.DefaultModuleDef.TopLevelDecls[i];
                curDecl = d as ClassDecl;
                if (curDecl != null)
                {
                    // scan each member for tactic calls and resolve if found

                    for (int j = 0; j < curDecl.Members.Count; j++)
                    {
                        MemberDecl md = curDecl.Members[j];
                        if (md is Tactic)
                            tactics.Add(md.Name, (Tactic)md);
                        else
                            members.Add(md);
                    }
                }
                else
                {
                    DatatypeDecl dd = d as DatatypeDecl;
                    if (dd != null)
                        globals.Add(dd);
                }
            }

            if (tactics.Count > 0)
            {
                foreach (MemberDecl md in members)
                {
                    err = ScanMemberBody(md);
                    if (err != null)
                        return err;
                }

                if (solution_list != null)
                {
                    err = VerifySolutionList(solution_list, ref prg);
                    if (err != null)
                        tacnyProgram.program = prg;

                    return err;
                }
            }
            tacnyProgram.ResolveProgram();
            tacnyProgram.VerifyProgram();
            return err;
        }

        /// <summary>
        /// Traverses the tree. Generates, resolves and verifies each leaf node until a 
        /// valid proof is found
        /// </summary>
        /// <param name="solution_tree"></param>
        /// <returns></returns>
        private string VerifySolutionList(SolutionList sol_tree, ref Dafny.Program result)
        {
            string err = null;
            result = null;
            Dafny.Program prog = tacnyProgram.parseProgram();
            foreach (var item in sol_tree.plist)
            {
                if (!item.isFinal)
                    return "VerifySolutionTree: Received non final solution";
                item.GenerateProgram(ref prog);
            }
            err = tacnyProgram.ResolveProgram(prog);
            if (err != null)
                return err;
            tacnyProgram.MaybePrintProgram(prog, DafnyOptions.O.DafnyPrintResolvedFile);
            tacnyProgram.VerifyProgram(prog);
            if (tacnyProgram.stats.ErrorCount == 0)
            {
                result = prog;
                return "done";
            }

            if (result != null)
                return null;
            return "VerifySolution: Unable to verify the generated soution";
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

                    if (tactics.ContainsKey(name))
                    {
                        string err = ResolveTacticBody(tactics[name], st as UpdateStmt, md); // generate a solution tree
                        if (err != null)
                            return err;
                    }
                }
            }

            return null;
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

            //local solution list
            SolutionList solution_list = new SolutionList(new Solution(new Action(md, tac, tac_call, tacnyProgram, globals)));
            string err = null;


            while (!solution_list.IsFinal())
            {
                List<Solution> result = null;
                foreach (var solution in solution_list.plist)
                {
                    err = solution.state.ResolveOne(ref result, solution);
                    if (err != null)
                        return err;
                    foreach (var res in result)
                        res.parent = solution;
                }

                if (result.Count > 0)
                    solution_list.AddRange(result);
                else
                    solution_list.SetIsFinal();
            }

            this.solution_list.AddFinal(solution_list.plist);
            return null;
        }
    }
}
