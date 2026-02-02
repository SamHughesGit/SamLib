using System;
using System.Collections.Generic;
using System.Text;

namespace SamLib.Demo
{
    /// <summary>
    /// Rock paper scissors
    /// </summary>
    public static class RPS 
    {
        /// <summary>
        /// Get RPS winner
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns>-1 error, 0 draw, 1 p1, 2 p2</returns>
        public static int GetWinner(string p1, string p2)
        {
            if (p1 == "rock")
            {
                return (p2 == "paper" ? 2 : p2 == "rock" ? 0 : 1);
            }
            else if (p1 == "paper")
            {
                return (p2 == "paper" ? 0 : p2 == "rock" ? 1 : 2);
            }
            else if (p1 == "scissors")
            {
                return (p2 == "paper" ? 1 : p2 == "rock" ? 2 : 0);
            }
            else { return -1; }
        }
    }
}
