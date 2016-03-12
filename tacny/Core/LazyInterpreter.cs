using System;
using System.Collections.Generic;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using System.Diagnostics.Contracts;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LazyTacny
{
    public class Interpreter
    {
        private Tacny.Program tacnyProgram;
        private SolutionList solution_list = new SolutionList();

        public Interpreter(Tacny.Program tacnyProgram)
        {
            Contract.Requires(tacnyProgram != null);
            this.tacnyProgram = tacnyProgram;
            this.solution_list = new SolutionList();
            //Console.SetOut(System.IO.TextWriter.Null);
        }

        public Dafny.Program ResolveProgram()
        {
            Debug.Indent();
            if (tacnyProgram.tactics.Count < 1)
            {
                return tacnyProgram.ParseProgram();
            }
            if (Util.TacnyOptions.O.ParallelExecution)
            {
                Parallel.ForEach(tacnyProgram.members, (member) =>
            {
                if (Thread.CurrentThread.Name == null)
                    Thread.CurrentThread.Name = member.Value.Name;
                var res = LazyScanMemberBody(tacnyProgram.NewProgram(), member.Value);
                if (res != null)
                {

                    solution_list.Add(res);
                    solution_list.Fin();

                }
            });
            }
            else {
                foreach (var member in tacnyProgram.members)
                {
                    var res = LazyScanMemberBody(tacnyProgram.NewProgram(), member.Value);
                    if (res != null)
                    {
                        solution_list.Add(res);
                        solution_list.Fin();

                    }
                }
            }
            // temp hack
            List<Solution> final = new List<Solution>();
            foreach (var solution in solution_list.GetFinal())
                final.Add(solution[0]);

            Dafny.Program prog = tacnyProgram.ParseProgram();
            foreach (var solution in final)
                solution.GenerateProgram(ref prog);
            tacnyProgram.dafnyProgram = prog;

            Debug.Unindent();
            return prog;

        }


        private Solution LazyScanMemberBody(Tacny.Program prog, MemberDecl md)
        {
            Contract.Requires(md != null);

            Debug.WriteLine(String.Format("Scanning member {0} body", md.Name));
            Method m = md as Method;
            if (m == null)
                return null;
            if (m.Body == null)
                return null;

            List<IVariable> variables = new List<IVariable>();

            foreach (var st in m.Body.Body)
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
                        Debug.WriteLine("Tactic call found");
                        try
                        {
                            Tactic tac = prog.GetTactic(us);
                            prog.SetCurrent(tac, md);
                            variables.AddRange(m.Ins);
                            variables.AddRange(m.Outs);
                            SolutionList sol_list = new SolutionList();
                            //sol_list.AddRange(solution_list.plist);
                            // get the resolved variables
                            List<IVariable> resolved = prog.GetResolvedVariables(md);
                            resolved.AddRange(m.Ins); // add input arguments as resolved variables
                            Solution result = Atomic.ResolveTactic(tac, us, md, prog, variables, resolved, sol_list);
                            Debug.IndentLevel = 0;
                            lock (this)
                            {
                                prog.currentDebug.PrintDebugData(prog);
                            }
                            return result;
                        }
                        catch (Exception e)
                        {
                            Util.Printer.Error(e.Message);
                            return null;
                        }
                    }
                }
            }
            return null;
        }
    }
}
