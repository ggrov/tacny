using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;

namespace Tacny
{

    /// <summary>
    /// Conditional actions should only be called from composition action
    /// </summary>
    class ConditionalAction : Action
    {
        public class ConditionResult
        {
            public readonly bool success;
            public ConditionResult(bool success)
            {
                this.success = success;
            }
        }

        public ConditionalAction(Action action) : base(action) { }

        /// <summary>
        /// Forces current node verification
        /// </summary>
        /// <param name="solution_tree"></param>
        /// <returns></returns>
        public string IsValid(ref SolutionTree solution_tree, out ConditionResult result)
        {
            string err;
            if (!solution_tree.isLeaf())
                solution_tree = solution_tree.GetLeftMost();

            Dafny.Program prog = program.NewProgram();
            err = solution_tree.GenerateProgram(ref prog);
            err = program.ResolveProgram(prog);
            program.VerifyProgram(prog);
            
            if (program.stats.ErrorCount == 0)
                result = new ConditionResult(true);
            else
                result = new ConditionResult(false);


            return null;
        }
        
    }
}
