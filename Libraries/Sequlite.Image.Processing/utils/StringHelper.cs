using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.Image.Processing.utils
{
    public class StringHelper
    {
        public static T ParseOrDefault<T>(string value)
        {
            return ReferenceEquals(value, null)
                 ? default(T) : (T)Convert.ChangeType(value, typeof(T));
        }

        // Converts a string to an array and copies that array to another (pre-initialized) array
        // Example: Input: "7 2 10 14" and [1,2,3,4,5,6,7] Output: [7,2,10,14,5,6,7]
        public static void StringToInitializedArray<T>(string input, T[] initArray, char separator = ' ')
        {
            T[] inputArray = Array.ConvertAll(input.Trim('\"').Trim().Split(separator), ParseOrDefault<T>);
            Debug.Assert(initArray.Length >= inputArray.Length);
            Array.Copy(inputArray, initArray, inputArray.Length);
        }
        public static void StringToInitializedArray<T>(string input, string target, char separator = ' ')
        {
            T[] inputArray = Array.ConvertAll(input.Trim('\"').Trim().Split(separator), ParseOrDefault<T>);
            T[] targetArray = Array.ConvertAll(target.Trim('\"').Trim().Split(separator), ParseOrDefault<T>);
            Debug.Assert(targetArray.Length >= inputArray.Length);
            Array.Copy(inputArray, targetArray, inputArray.Length);
        }
    }
}
