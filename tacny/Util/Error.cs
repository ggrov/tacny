using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Microsoft.Boogie;

namespace Util
{
    public static class Error
    {
        /// <summary>
        /// Create an error message
        /// </summary>
        /// <param name="tok">Token of the error location</param>
        /// <param name="n">error index</param>
        /// <param name="args">Optional string formating params</param>
        /// <returns>Error message</returns>
        public static string MkErr(IToken tok, int n, params object[] args)
        {
            Contract.Requires(tok != null);
            return string.Format("{0}({1},{2}): Error: {3}",
                DafnyOptions.Clo.UseBaseNameForFileName ? System.IO.Path.GetFileName(tok.filename) : tok.filename, tok.line, tok.col - 1,
                string.Format(GetErrorString(n), args));
        }
        /// <summary>
        /// Create an error message 
        /// </summary>
        /// <param name="d">Declaration</param>
        /// <param name="n">error index</param>
        /// <param name="args">Optional string formating params</param>
        /// <returns>Error message</returns>
        public static string MkErr(Microsoft.Dafny.Declaration d, int n, params object[] args)
        {
            Contract.Requires(d != null);
            return MkErr(d.tok, n, args);
        }

        /// <summary>
        /// Create an error message 
        /// </summary>
        /// <param name="s">Statement</param>
        /// <param name="n">error index</param>
        /// <param name="args">Optional string formating params</param>
        /// <returns>Error message</returns>
        public static string MkErr(Statement s, int n, params object[] args)
        {
            Contract.Requires(s != null);
            return MkErr(s.Tok, n, args);
        }

        /// <summary>
        /// Create an error message 
        /// </summary>
        /// <param name="v">NonglobalVariable</param>
        /// <param name="n">error index</param>
        /// <param name="args">Optional string formating params</param>
        /// <returns>Error message</returns>
        public static string MkErr(NonglobalVariable v, int n, params object[] args)
        {
            Contract.Requires(v != null);
            return MkErr(v.tok, n, args);
        }

        /// <summary>
        /// Create an error message 
        /// </summary>
        /// <param name="e">Expression</param>
        /// <param name="n">error index</param>
        /// <param name="args">Optional string formating params</param>
        /// <returns>Error message</returns>
        public static string MkErr(Expression e, int n, params object[] args)
        {
            Contract.Requires(e != null);
            return MkErr(e.tok, n, args);
        }


        static string GetErrorString(int n)
        {
            string s;
            switch(n)
            {
                case 0: s = "Unexpected number of arguments. Expected: {0} Received: {1}"; break;
                case 1: s = "Unexpected argument type: Expected {0}"; break;
                case 2: s = "Could not extract the statement guard"; break;
                case 3: s = "Could not find the target method"; break;
                case 4: s = "The atom can only be called from a method"; break;
                case 5: s = "Unexpected statement type. Expected {0} Received {1}"; break;
                case 6: s = "{0} is not defined in current context"; break;
                case 7: s = "Variable missmatch"; break;
                case 8: s = "Missing variable assignment"; break;
                case 9: s = "Variable {0} is not declared"; break;
                case 10: s = "Cound not process input argument"; break;
                case 11: s = "Statement called outside while loop"; break;
                case 12: s = "Datatype {0} is not defined"; break;
                case 13: s = "Tacny variables must be declared as tvar"; break;
                case 14: s = "The precondition failed"; break;
                case 15: s = "{0} can only be called from a (ghost) method"; break;
                case 16: s = "Solution does not exist"; break;
                default: s = "error" + n; break;
            }

            return s;
        }


    }
}
