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
            string datatype_name = null;
            DatatypeDecl datatype = null;
            IVariable declaration = null;
            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(tcce.OfSize(call_arguments, 1), Util.Error.MkErr(st, 0, 1, call_arguments.Count));

            NameSegment argument = call_arguments[0] as NameSegment;
            Contract.Assert(argument != null, Util.Error.MkErr(st, 1, typeof(NameSegment)));
            object val = GetLocalValueByName(argument);
            if (val is IVariable)
            {
                declaration = val as IVariable;
            }
            if (val != null)
            {
                string asd = null;
            }
            // get the formal tactic input to determine the type
            Dafny.LiteralExpr lit = new Dafny.LiteralExpr(st.Tok, false);
            foreach (var ctor in datatype.Ctors)
            {
                foreach (var formal in ctor.Formals)
                {
                    if (formal.Type.ToString() == datatype_name)
                    {
                        lit = new Dafny.LiteralExpr(st.Tok, true);
                        break;
                    }
                }
            }

            localContext.AddLocal(lv, lit);
        }
    }
}
