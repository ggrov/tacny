using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace Tacny
{
    class IsInductiveAtomic : Atomic, IAtomicStmt
    {
        public IsInductiveAtomic(Atomic atomic) : base(atomic) { }

        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            List<Expression> call_arguments = null;
            IVariable lv = null;
            string datatype_name = null;
            DatatypeDecl datatype = null;
            
            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(tcce.OfSize(call_arguments, 1), Error.MkErr(st, 0, 1, call_arguments.Count));
            
            NameSegment argument = call_arguments[0] as NameSegment;
            Contract.Assert(argument != null, Error.MkErr(st, 1, typeof(NameSegment)));

            // get the formal tactic input to determine the type
            Formal tac_input = (Formal)GetLocalKeyByName(argument);
            Contract.Assert(tac_input != null, Error.MkErr(st, 9, argument.Name));
            
            datatype_name = tac_input.Type.ToString();
            /**
             * TODO cleanup
             * if datatype is Element lookup the formal in global variable registry
             */
            if (datatype_name == "Element")
            {
                // get the original variable declaration
                object val = GetLocalValueByName(argument.Name);
                NameSegment decl = val as NameSegment;

                Contract.Assert(decl != null, Error.MkErr(st, 9, argument.Name));

                IVariable original_decl = globalContext.GetGlobalVariable(decl.Name);
                Contract.Assert(original_decl != null, Error.MkErr(st, 9, tac_input.Name));
                
                datatype_name = original_decl.Type.ToString();

                //UserDefinedType udt = original_decl.Type as UserDefinedType;
                //if (udt != null)
                //    datatype_name = udt.Name;
                //else
                //    datatype_name = original_decl.Type.ToString();
            }
            else
                Contract.Assert(false, Error.MkErr(st, 1, "Element"));

            Contract.Assert(datatype_name != null);
            if (!globalContext.ContainsGlobalKey(datatype_name))
                Contract.Assert(false, Error.MkErr(st, 12, datatype_name));

            argument = GetLocalValueByName(tac_input) as NameSegment;

            datatype = globalContext.GetGlobal(datatype_name);
            LiteralExpr lit = new LiteralExpr(st.Tok, false);
            foreach (var ctor in datatype.Ctors)
            {
                foreach (var formal in ctor.Formals)
                {
                    if (formal.Type.ToString() == datatype_name)
                    {
                        lit = new LiteralExpr(st.Tok, true);
                        break;
                    }
                }
            }

            localContext.AddLocal(lv, lit);
        }
    }
}
