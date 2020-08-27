using Microsoft.Extensions.Logging;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace PlatformFunctions.Helpers
{
    public static class Extensions
    {
        /// <summary>
        /// Log if any properties of <paramref name="checkValues"/> are null.
        /// </summary>
        /// <param name="log">this <see cref="ILogger"/></param>
        /// <param name="level">level at which to log</param>
        /// <param name="checkValues">object with properties to check</param>
        /// <param name="nulls">will output the name(s) of any null properties</param>
        /// <param name="message">can be included in warning log, optional</param>
        /// <param name="method">Auto-populated; do not use unless overloading.</param>
        /// <param name="line">Auto-populated; do not use unless overloading.</param>
        /// <returns><c>True</c> if there are any null properties.</returns>
        /// <returns></returns>
        public static bool CheckNull(this ILogger log, LogLevel level, object checkValues, out string nulls, string message,
            [CallerMemberName] string method = "", [CallerLineNumber] int line = -1)
        {
            if (checkValues is null) return CheckNull(log, level, new { checkValues }, out nulls, message, method, line);

            nulls = checkValues.GetType().GetProperties()
                .Aggregate(new StringBuilder(), (builder, property) => property.GetValue(checkValues) is null ? builder.Append(property.Name).Append(',').Append(' ') : builder)
                .ToString().Trim(',', ' ');
            if (nulls != string.Empty)
            {
                log?.Log(level, "{0}: {1}: Value(s) {2} null at line {3}.", method, message, nulls, line);
                return true;
            }
            return false;
        }
    }
}
