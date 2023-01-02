using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.RecipeLib
{
    public class RecipeHelpers
    {
        /// <summary>
        /// Detect if a process value has overshot its setpoint.
        /// 
        /// Overshoot is defined as: 
        /// The process value is increasing and is above the setpoint or
        /// The process value is decreasing and is below the setpoint.
        /// 
        /// </summary>
        /// <param name="start">The process start point</param>
        /// <param name="current">The current process value</param>
        /// <param name="target">The process setpoint</param>
        /// <returns>True if the current value has gone "through" the target. Always returns
        /// false if the current value is equal to the start value (slope = 0).</returns>
        public static bool HasOvershot(in double start, in double current, in double target)
        {
            if(start < target && current < start)
            {
                return false;
            }
            if(start > target && current > start)
            {
                return false;
            }
            double deltaStart = current - start;
            double deltaTarget = current - target;

            // compare booleans and return true if both deltas have the same sign
            // if the process is stable or at the setpoint always return false
            return !deltaStart.Equals(0.0) && !deltaTarget.Equals(0.0) && ((deltaStart < 0) == (deltaTarget < 0));
        }
    }
}
