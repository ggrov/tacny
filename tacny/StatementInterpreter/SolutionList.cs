    using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;
using Bpl = Microsoft.Boogie;
using Tacny;

namespace Tacny
{
    public class SolutionList
    {

        public List<Solution> plist;

        private List<List<Solution>> final; // a list of solutions ofr each tactic

        public SolutionList()
        {
            plist = new List<Solution>();
            final = new List<List<Solution>>();
        }

        public SolutionList(Solution solution)
        {
            Contract.Requires(solution != null);
            plist = new List<Solution>() { solution };
            final = new List<List<Solution>>();
        }

        public void Add(Solution solution)
        {
            plist.Add(solution);
        }

        public void AddRange(List<Solution> solutions)
        {
            plist.Clear();
            plist = solutions;
        }

        public void AddFinal(List<Solution> solutions)
        {
            final.Add(solutions);
        }

        public bool IsFinal()
        {
            foreach (var item in plist)
                if (!item.isFinal)
                    return false;
            return true;
        }

        public void SetIsFinal()
        {
            foreach (var item in plist)
                item.isFinal = true;
        }

        public void UnsetFinal()
        {
            foreach (var item in plist)
                item.isFinal = false;
        }

        public List<List<Solution>> GetFinal()
        {
            return final;
        }
    }


    public class Solution
    {
        public Action state;
        private Solution _parent = null;
        public Solution parent
        {
            set { Contract.Requires(_parent == null); _parent = value; }
            get { return _parent; }
        }

        public bool isFinal = false;

        public Solution(Action state, Solution parent = null)
            : this(state, false, parent)
        { }

        public Solution(Action state, bool isFinal, Solution parent)
        {
            this.state = state;
            this.isFinal = isFinal;
            this.parent = parent;
        }

        public string GenerateProgram(ref Dafny.Program prog)
        {

            List<Dafny.Program> prog_list = new List<Dafny.Program>();
            Action ac = state.Copy();
            ac.Fin();
            Method method = (Method)Program.FindMember(prog, ac.md.Name);
            if (method == null)
                throw new Exception("Method not found");
            UpdateStmt tac_call = ac.tac_call;
            List<Statement> body = method.Body.Body;
            body = InsertSolution(body, tac_call, ac.resolved);
            if (body == null)
                throw new Exception("Body not filled");

            Method nMethod = new Method(method.tok, method.Name, method.HasStaticKeyword, method.IsGhost,
                method.TypeArgs, method.Ins, method.Outs, method.Req, method.Mod, method.Ens, method.Decreases,
                new BlockStmt(method.Body.Tok, method.Body.EndTok, body), method.Attributes, method.SignatureEllipsis);
            ClassDecl curDecl;

            for (int i = 0; i < prog.DefaultModuleDef.TopLevelDecls.Count; i++)
            {
                curDecl = prog.DefaultModuleDef.TopLevelDecls[i] as ClassDecl;
                if (curDecl != null)
                {
                    // scan each member for tactic calls and resolve if found
                    for (int j = 0; j < curDecl.Members.Count; j++)
                    {
                        Method old_m = curDecl.Members[j] as Method;
                        if (old_m != null)
                            if (old_m.Name == nMethod.Name)
                                curDecl.Members[j] = nMethod;

                    }
                    prog.DefaultModuleDef.TopLevelDecls[i] = RemoveTactics(curDecl);
                }
            }

            return null;
        }

        private static List<Statement>  InsertSolution(List<Statement> body, UpdateStmt tac_call, List<Statement> solution)
        {
            WhileStmt ws = null;
            BlockStmt bs = null;
            int index = FindTacCall(body, tac_call);
            if (index == -1)
                return null;

            List<Statement> newBody = new List<Statement>();
            if (index == 0)
            {
                Statement[] tmp = body.ToArray();
                newBody = new List<Statement>(tmp);
                newBody.RemoveAt(index);
                newBody.InsertRange(0, solution);
                return newBody;
            }

            // check where from tac_call has been made
            int i = index + 1;
            while (i < body.Count)
            {
                Statement stmt = body[i];
                bs = stmt as BlockStmt;
                // if we found a block statement check behind to find the asociated while statement
                if (bs != null)
                {
                    int j = index;
                    while (j >= 0)
                    {
                        Statement stmt_2 = body[j];
                        ws = stmt_2 as WhileStmt;
                        if (ws != null)
                            break;

                        else if (!(stmt_2 is UpdateStmt))
                            return null;

                        j--;
                    }
                    break;
                }
                else if (!(stmt is UpdateStmt))
                    return null;

                i++;
            }
            //// tactic called from a while statement
            if (ws != null && bs != null)
            {
                Statement[] tmp = body.ToArray();
                int l_bound = body.IndexOf(ws);
                int u_boud = body.IndexOf(bs);
                // tactic called in a while statement should 
                //  return only a single solution item which is a WhileStmt
                if (solution.Count > 0)
                {
                    WhileStmt mod_ws = (WhileStmt)solution[0];
                    mod_ws = new WhileStmt(mod_ws.Tok, mod_ws.EndTok, mod_ws.Guard, mod_ws.Invariants,
                        mod_ws.Decreases, mod_ws.Mod, bs);
                    tmp[l_bound] = mod_ws;
                }
                else
                {
                    ws = new WhileStmt(ws.Tok, ws.EndTok, ws.Guard, ws.Invariants, ws.Decreases, ws.Mod, bs);
                    tmp[l_bound] = ws;
                }
                l_bound++;


                // for now remove everything between while stmt and block stmt
                while (l_bound <= u_boud)
                {
                    tmp[l_bound] = null;
                    l_bound++;
                }

                foreach (Statement st in tmp)
                    if (st != null)
                        newBody.Add(st);
            }
            else
            {
                Statement[] tmp = body.ToArray();
                newBody = new List<Statement>(tmp);
                newBody.RemoveAt(index);
                newBody.InsertRange(index, solution);
            }

            return newBody;
        }

        private static int FindTacCall(List<Statement> body, UpdateStmt tac_call)
        {
            for (int j = 0; j < body.Count; j++)
            {
                UpdateStmt us = body[j] as UpdateStmt;
                if (us != null)
                    if (us.Tok.line == tac_call.Tok.line && us.Tok.col == tac_call.Tok.col)
                        return j;
            }
            return -1;
        }

        private static TopLevelDecl RemoveTactics(ClassDecl cd)
        {
            List<MemberDecl> mdl = new List<MemberDecl>();
            foreach (MemberDecl md in cd.Members)
                if (!(md is Tactic))
                    mdl.Add(md);

            return new ClassDecl(cd.tok, cd.Name, cd.Module, cd.TypeArgs, mdl, cd.Attributes, cd.TraitsTyp);
        }


    }
}
