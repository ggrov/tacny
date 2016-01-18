using System;
using System.Collections.Generic;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using System.Diagnostics.Contracts;

namespace Tacny
{
    public class Interpreter
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
                    VerifySolutionList(solution_list);
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
        private void VerifySolutionList(SolutionList solution_list)
        {
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
            // print debug data
            end = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            if (final.Count > 0)
            {
                tacnyProgram.PrintDebugMessage("Execution time {0}s\nGenerated: {1} branches\nInvalid: {2} branches\nFailed to verify {3} branches",
                    end - start,
                    final[0].state.GetTotalBranchCount(),
                    final[0].state.GetInvalidBranchCount(),
                    final[0].state.GetBadBranchCount());
            }
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
                        try
                        {
                            Atomic.ResolveTactic(tacnyProgram.GetTactic(us), us, md, tacnyProgram, variables, ref sol_list);
                            
                        }
                        catch (Exception e)
                        {
                            return e.Message;
                        }
                    }
                }
            }


            solution_list.AddRange(sol_list.plist);

            return null;
        }
    }
}
