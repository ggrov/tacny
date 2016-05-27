using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using Microsoft.Dafny;
using Util;
using Printer = Util.Printer;

namespace Tacny
{
    public class Interpreter
    {
        private Program tacnyProgram;
        private SolutionList solution_list = new SolutionList();

        public Interpreter(Program tacnyProgram)
        {
            Contract.Requires(tacnyProgram != null);
            this.tacnyProgram = tacnyProgram;
            solution_list = new SolutionList();
            //Console.SetOut(System.IO.TextWriter.Null);
        }

        public string ResolveProgram()
        {
            string err = null;

            if (tacnyProgram.Tactics.Count > 0)
            {
                foreach (var member in tacnyProgram.Members)
                {
                    err = ScanMemberBody(member.Value);
                    solution_list.Fin();

                    if (err != null)
                        return err;
                }

                if (solution_list != null)
                {
                    VerifySolutionList();
                    return null;
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
        private void VerifySolutionList()
        {
            List<Solution> final = new List<Solution>(); // list of verified solutions
            Microsoft.Dafny.Program program;
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
                    tacnyProgram.MaybePrintProgram("debug" + i);
                    tacnyProgram.ClearBody(solution.state.globalContext.md);
                    
                    tacnyProgram.VerifyProgram();
                    
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
        }

        private string ScanMemberBody(MemberDecl md)
        {
            Contract.Requires(md != null);
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
            if (TacnyOptions.O.ParallelExecution)
            {
                Parallel.ForEach(m.Body.Body, st =>
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
                                try
                                {
                                    tacnyProgram.SetCurrent(tacnyProgram.GetTactic(us), md);
                                    // get the resolved variables
                                    List<IVariable> resolved = tacnyProgram.GetResolvedVariables(md);
                                    resolved.AddRange(m.Ins); // add input arguments as resolved variables
                                    Atomic.ResolveTactic(tacnyProgram.GetTactic(us), us, md, tacnyProgram, variables, resolved, ref sol_list);
                                    tacnyProgram.CurrentDebug.Fin();
                                }
                                catch (AggregateException e)
                                {
                                    foreach (var err in e.Data)
                                    {
                                        Printer.Error(err.ToString());
                                    }
                                }
                            }
                        }
                    });
            }
            else
            {
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
                            try
                            {
                                tacnyProgram.SetCurrent(tacnyProgram.GetTactic(us), md);
                                // get the resolved variables
                                List<IVariable> resolved = tacnyProgram.GetResolvedVariables(md);
                                resolved.AddRange(m.Ins); // add input arguments as resolved variables
                                Atomic.ResolveTactic(tacnyProgram.GetTactic(us), us, md, tacnyProgram, variables, resolved, ref sol_list);
                                tacnyProgram.CurrentDebug.Fin();
                            }
                            catch (Exception e)
                            {
                                return e.Message;
                            }
                        }
                    }
                }
            }
            solution_list.AddRange(sol_list.plist);
            return null;
        }
    }
}
