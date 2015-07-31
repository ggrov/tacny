using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;
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
            public readonly ErrorInformation errorInfo;
            public ConditionResult(bool success, ErrorInformation errorInfo)
            {
                this.success = success;
                this.errorInfo = errorInfo;
            }
        }

        public ConditionalAction(Action action) : base(action) { }

        /// <summary>
        /// Check whehter current solution is valid
        /// </summary>
        /// <param name="solution_list"></param>
        /// <returns></returns>
        public string IsValid(out ConditionResult result)
        {
            string err;
            
            Dafny.Program prog = program.parseProgram();
            err = solution.GenerateProgram(ref prog);
            err = program.ResolveProgram(prog);
            program.VerifyProgram(prog);
            
            if (program.stats.ErrorCount == 0)
                result = new ConditionResult(true, program.errorInfo);
            else
                result = new ConditionResult(false, program.errorInfo);


            return null;
        }
        
    }
}
