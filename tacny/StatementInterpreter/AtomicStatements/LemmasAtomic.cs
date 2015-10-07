using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;
namespace Tacny
{
    class LemmasAtomic : Atomic, IAtomicStmt
    {
        public LemmasAtomic(Atomic atomic) : base(atomic) { }



        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            return Lemmas(st, ref solution_list);
        }

        private string Lemmas(Statement st, ref List<Solution> solution_list)
        {
            string err;
            IVariable lv = null;
            List<Expression> call_arguments; // we don't care about this
            List<Lemma> lemmas = new List<Lemma>();

            err = InitArgs(st, out lv, out call_arguments);
            if (err != null)
                return "ERROR lemmas: " + err;

            if (call_arguments.Count != 0)
                return "ERROR lemmas:  the call does not take any arguments";

            foreach (var member in program.members.Values)
            {
                Lemma lem = member as Lemma;
                if (lem != null)
                    lemmas.Add(lem);
                
            }
            IncTotalBranchCount();
            AddLocal(lv, lemmas);
            solution_list.Add(new Solution(this.Copy()));
            return null;
        }
    }
}
