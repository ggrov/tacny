using System;
using System.Diagnostics;
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

        public static void Warning(string programId, string msg)
        {
            Contract.Requires(msg != null);
            ConsoleColor col = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("{0}: Warning: {1}", string.Format(programId), string.Format(msg));
            Console.ForegroundColor = col;
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
        private Program tacnyProgram;
        private int start;
        private int end;

        private SolutionList solution_list = new SolutionList();

        public Interpreter(Program tacnyProgram)
        {
            Contract.Requires(tacnyProgram != null);
            this.tacnyProgram = tacnyProgram;
            this.solution_list = new SolutionList();
            start = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }

        public string ResolveProgram()
        {
            string err = null;

            if (tacnyProgram.tactics.Count > 0)
            {
                foreach (var member in tacnyProgram.members)
                {
                    err = ScanMemberBody(member.Value);
                    solution_list.Fin();

                    if (err != null)
                        return err;
                }

                if (solution_list != null)
                {
                    err = VerifySolutionList(solution_list);

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
        private string VerifySolutionList(SolutionList solution_list)
        {
            string err = null;

            List<Solution> final = new List<Solution>(); // list of verified solutions
            Dafny.Program program;
            foreach (var list in solution_list.GetFinal())
            {
                int index = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    index = i;
                    var solution = list[i];
                    if (solution.isFinal)
                    {
                        final.Add(solution);
                        break;
                    }

                    program = tacnyProgram.ParseProgram();
                    solution.GenerateProgram(ref program);

                    tacnyProgram.ClearBody(solution.state.globalContext.md);

                    err = tacnyProgram.VerifyProgram();
                    if (err != null)
                        Warning(tacnyProgram.programId, err);
                    tacnyProgram.MaybePrintProgram(DafnyOptions.O.DafnyPrintResolvedFile);
                    if (!tacnyProgram.HasError())
                    {
                        final.Add(solution);
                        break;
                    }
                    if (index == list.Count - 1)
                        final.Add(solution);
                }
            }


            program = tacnyProgram.ParseProgram();
            foreach (var solution in final)
                solution.GenerateProgram(ref program);
            //err = tacnyProgram.VerifyProgram();
            end = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            if (final.Count > 0)
            {
                tacnyProgram.PrintDebugMessage("Execution time {0}s\nTotal branch count {1} branches\nInvalid branch count {2} branches",
                    end - start,
                    final[0].state.GetTotalBranchCount(),
                    final[0].state.GetBadBranchCount());
            }
            if (err != null)
                return err;



            return null;
        }

        private string ScanMemberBody(MemberDecl md)
        {
            solution_list.plist.Clear();
            Method m = md as Method;
            if (m == null)
                return null;
            if (m.Body == null)
                return null;
            List<IVariable> variables = new List<IVariable>();
            variables.AddRange(m.Ins);
            variables.AddRange(m.Outs);
            SolutionList sol_list = new SolutionList();
            sol_list.AddRange(solution_list.plist);
            foreach (Statement st in m.Body.Body)
            {
                // register local variables
                VarDeclStmt vds = st as VarDeclStmt;
                if (vds != null)
                    variables.AddRange(vds.Locals);

                UpdateStmt us = st as UpdateStmt;
                if (us != null)
                {
                    if (tacnyProgram.IsTacticCall(us))
                    {
                        string err = Atomic.ResolveTactic(tacnyProgram.GetTactic(us), us, md, tacnyProgram, variables, ref sol_list);
                        if (err != null)
                            return err;
                    }
                }
            }


            solution_list.AddRange(sol_list.plist);

            return null;
        }
    }
}
