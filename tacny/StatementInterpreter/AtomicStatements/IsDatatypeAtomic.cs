using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using Dafny = Microsoft.Dafny;

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
            Dafny.LiteralExpr lit =null;
            Dafny.Type type = null;

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(tcce.OfSize(call_arguments, 1), Util.Error.MkErr(st, 0, 1, call_arguments.Count));
            
            NameSegment argument = call_arguments[0] as NameSegment;
            Contract.Assert(argument != null, Util.Error.MkErr(st, 1, typeof(NameSegment)));

            declaration = GetLocalValueByName(argument) as IVariable;
            Contract.Assert(declaration != null, Util.Error.MkErr(st, 1, typeof(IVariable)));

            if (declaration.Type == null)
                type = globalContext.GetVariableType(declaration.Name);
            else
                type = declaration.Type;
            // type of the argument is unknown thus it's not a datatype
            if (type != null && type.IsDatatype)
                lit = new Dafny.LiteralExpr(st.Tok, true);
            else
            {
                // check if the argument is a nested data type
                Dafny.UserDefinedType udt = type as Dafny.UserDefinedType;
                if (udt != null)
                {
                    if(globalContext.datatypes.ContainsKey(udt.Name))
                        lit = new Dafny.LiteralExpr(st.Tok, true);
                }
                else
                    lit = new Dafny.LiteralExpr(st.Tok, false);
            }

            localContext.AddLocal(lv, lit);
        }
    }
}
