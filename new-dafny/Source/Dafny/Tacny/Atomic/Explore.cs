using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Formal = Microsoft.Dafny.Formal;
using Type = Microsoft.Dafny.Type;


namespace Tacny.Atomic {

  class Explore : Atomic {
    public override string Signature => "explore";

    public override int ArgsCount => 2;

    public override IEnumerable<ProofState> Generate(Statement statement, ProofState state) {
      List<List<IVariable>> args = new List<List<IVariable>>();
      List<IVariable> mdIns = new List<IVariable>();
      List<Expression> callArguments;
      IVariable lv;
      InitArgs(state, statement, out lv, out callArguments);

      state.IfVerify = true;


      //TODO: implement this properly
      //var members = state.GetLocalValue(callArguments[0] as NameSegment) as IEnumerable<MemberDecl>;
      //evaluate the argument (methods/lemma)
      var members0 = Interpreter.EvalTacnyExpression(state, callArguments[0]).GetEnumerator();
      members0.MoveNext();
      var members = members0.Current as List<MemberDecl>;

      if (members == null){
        yield break;
      }

      foreach(var member in members) {
        MemberDecl md;
        mdIns.Clear();
        args.Clear();

        if(member is NameSegment) {
          //TODO:
          Console.WriteLine("double check this");
          md = null;
          // md = state.GetDafnyProgram().  Members.FirstOrDefault(i => i.Key == (member as NameSegment)?.Name).Value;
        } else {
          md = member as MemberDecl;
        }

        // take the membed decl parameters
        var method = md as Method;
        if(method != null)
          mdIns.AddRange(method.Ins);
        else if(md is Microsoft.Dafny.Function)
          mdIns.AddRange(((Microsoft.Dafny.Function)md).Formals);
        else
          Contract.Assert(false, "In Explore Atomic call," + callArguments[0] + "is neither a Method or a Function");

        //evaluate the arguemnts for the lemma to be called
        var instArgs = Interpreter.EvalTacnyExpression(state, callArguments[1]);
        foreach(var ovars in instArgs) {
          Contract.Assert(ovars != null, "In Explore Atomic call," + callArguments[1] + "is not variable");

          List<IVariable> vars = ovars as List<IVariable> ?? new List<IVariable>();
          //Contract.Assert(vars != null, Util.Error.MkErr(call_arguments[0], 1, typeof(List<IVariable>)));

          //for the case when no args, just add an empty list
          if (mdIns.Count == 0){
            args.Add(new List<IVariable>());
          }
          for(int i = 0; i < mdIns.Count; i++) {
            var item = mdIns[i];
            args.Add(new List<IVariable>());
            foreach(var arg in vars) {
              // get variable type
              Type type = state.GetDafnyVarType(arg.Name);
              if(type != null) {
                if(type is UserDefinedType && item.Type is UserDefinedType) {
                  var udt1 = type as UserDefinedType;
                  var udt2 = item.Type as UserDefinedType;
                  if(udt1.Name == udt2.Name)
                    args[i].Add(arg);
                } else {
                  // if variable type and current argument types match, or the type is yet to be inferred
                  if(item.Type.ToString() == type.ToString() || type is InferredTypeProxy)
                    args[i].Add(arg);
                }
              } else
                args[i].Add(arg);
            }
            /**
             * if no type correct variables have been added we can safely return
             * because we won't be able to generate valid calls
             */
            if(args[i].Count == 0) {
              Debug.WriteLine("No type matching variables were found");
              yield break;
            }
          }

          foreach(var result in PermuteArguments(args, 0, new List<NameSegment>())) {
            // create new fresh list of items to remove multiple references to the same object
            List<Expression> newList = result.Cast<Expression>().ToList().Copy();
            //TODO: need to double check wirh Vito, why can't use copy ?
            //Util.Copy.CopyExpressionList(result.Cast<Expression>().ToList());
            ApplySuffix aps = new ApplySuffix(callArguments[0].tok, new NameSegment(callArguments[0].tok, md.Name, null),
              newList);
            if(lv != null) {
              var newState = state.Copy();
              newState.AddTacnyVar(lv, aps);
              yield return newState;
            } else {
              var newState = state.Copy();
              UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(),
                new List<AssignmentRhs> { new ExprRhs(aps) });
              //Printer p = new Printer(Console.Out);
              //p.PrintStatement(us,0);
              newState.AddStatement(us);
              yield return newState;
            }
          }
        }
      }
    }


    private static List<Expression> GetCallArguments(UpdateStmt us) {
      Contract.Requires(us != null);
      var er = (ExprRhs)us.Rhss[0];
      return ((ApplySuffix)er.Expr).Args;
    }
    private void InitArgs(ProofState ps, Statement st, out IVariable lv, out List<Expression> callArguments) {
      Contract.Requires(st != null);
      Contract.Ensures(Contract.ValueAtReturn(out callArguments) != null);
      lv = null;
      callArguments = null;
      TacticVarDeclStmt tvds;
      UpdateStmt us;
      TacnyBlockStmt tbs;

      // tacny variables should be declared as tvar or tactic var
      //if(st is VarDeclStmt)
      //  Contract.Assert(false, Error.MkErr(st, 13));

      if((tvds = st as TacticVarDeclStmt) != null) {
        lv = tvds.Locals[0];
        callArguments = GetCallArguments(tvds.Update as UpdateStmt);

      } else if((us = st as UpdateStmt) != null) {
        if(us.Lhss.Count == 0)
          callArguments = GetCallArguments(us);
        else {
          var ns = (NameSegment)us.Lhss[0];
          if(ps.ContainTacnyVal(ns)) {
            //TODO: need to doubel check this
            lv = ps.GetTacnyVarValue(ns) as IVariable;
            callArguments = GetCallArguments(us);
          }
        }
      } else if((tbs = st as TacnyBlockStmt) != null) {
        var pe = tbs.Guard as ParensExpression;
        callArguments = pe != null ? new List<Expression> { pe.E } : new List<Expression> { tbs.Guard };
      }

    }
    private static IEnumerable<List<NameSegment>> PermuteArguments(List<List<IVariable>> args, int depth, List<NameSegment> current) {
      if(args.Count == 0)
        yield break;
      if(depth == args.Count) {
        yield return current;
        yield break;
      }
      if (args[depth].Count == 0){
        yield return new List<NameSegment>();
        yield break;
      }
      for(int i = 0; i < args[depth].Count; ++i) {
        List<NameSegment> tmp = new List<NameSegment>();
        tmp.AddRange(current);
        IVariable iv = args[depth][i];
        NameSegment ns = new NameSegment(iv.Tok, iv.Name, null);
        tmp.Add(ns);
        foreach(var item in PermuteArguments(args, depth + 1, tmp))
          yield return item;
      }
    }

  }
}
