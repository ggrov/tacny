using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace Tacny
{
    class IsDatatypeAtomic : Atomic, IAtomicStmt
    {
        public IsDatatypeAtomic(Atomic atomic) : base(atomic) { }




        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            IsDatatype(st, ref solution_list);
        }


        private void IsDatatype(Statement st, ref List<Solution> solution_list)
        {
            List<Expression> call_arguments = null;
            IVariable lv = null;
            IVariable declaration = null;
            LiteralExpr lit =null;
            Type type = null;

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(tcce.OfSize(call_arguments, 1), Error.MkErr(st, 0, 1, call_arguments.Count));
            
            NameSegment argument = call_arguments[0] as NameSegment;
            Contract.Assert(argument != null, Error.MkErr(st, 1, typeof(NameSegment)));

            declaration = GetLocalValueByName(argument) as IVariable;
            Contract.Assert(declaration != null, Error.MkErr(st, 1, typeof(IVariable)));

            if (declaration.Type == null)
                type = globalContext.GetVariableType(declaration.Name);
            else
                type = declaration.Type;
            // type of the argument is unknown thus it's not a datatype
            if (type != null && type.IsDatatype)
                lit = new LiteralExpr(st.Tok, true);
            else
            {
                // check if the argument is a nested data type
                UserDefinedType udt = type as UserDefinedType;
                if (udt != null)
                {
                    if(globalContext.datatypes.ContainsKey(udt.Name))
                        lit = new LiteralExpr(st.Tok, true);
                    else
                        lit = new LiteralExpr(st.Tok, false);
                }
                else
                    lit = new LiteralExpr(st.Tok, false);
            }

            Contract.Assert(lit != null);
            localContext.AddLocal(lv, lit);
        }
    }
}
