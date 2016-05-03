using System;
using System.Collections.Generic;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using System.Diagnostics.Contracts;
using System.Diagnostics;


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

        public Program ResolveProgram()
        {

            if (!tacnyProgram.HasTacticApplications())
            {
                return tacnyProgram.ParseProgram();
            }
            foreach (var @class in tacnyProgram.topLevelClasses)
            {
                tacnyProgram.currentTopLevelClass = @class;
                if (tacnyProgram.currentTopLevelClass.tactics.Count < 1)
                    continue;
                foreach (var member in tacnyProgram.members)
                {
                    var res = LazyScanMemberBody(member.Value);
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
            {
                solution.GenerateProgram(ref prog);
            }
            tacnyProgram.dafnyProgram = prog;

            return prog;

        }


        private Solution LazyScanMemberBody(MemberDecl md)
        {
            Contract.Requires(md != null);

            Debug.WriteLine(String.Format("Scanning member {0} body", md.Name));
            if (md is Function)
            {
                var fun = md as Function;
                Tacny.ExpressionTree expt = null;
                if(fun.Body != null)
                    expt = Tacny.ExpressionTree.ExpressionToTree(fun.Body);
            }
            else if (md is Method)
            {
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
                        if (this.tacnyProgram.IsTacticCall(us))
                        {
                            Debug.WriteLine("Tactic call found");
                            try
                            {
                                ITactic tac = tacnyProgram.GetTactic(us);
                                tacnyProgram.SetCurrent(tac, md);
                                variables.AddRange(m.Ins);
                                variables.AddRange(m.Outs);
                                //sol_list.AddRange(solution_list.plist);
                                // get the resolved variables
                                List<IVariable> resolved = tacnyProgram.GetResolvedVariables(md);
                                Console.Out.WriteLine(string.Format("Resolving {0} in {1}", tac.Name, md.Name));
                                resolved.AddRange(m.Ins); // add input arguments as resolved variables
                                Solution result = Atomic.ResolveTactic(tac, us, md, tacnyProgram, variables, resolved);
                                Debug.IndentLevel = 0;
                                //                            Solution.PrintSolution(result);
                                this.tacnyProgram.currentDebug.Fin();
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
            }
            return null;
        }
    }
}
