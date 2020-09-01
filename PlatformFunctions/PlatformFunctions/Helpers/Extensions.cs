using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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

        /// <summary>
        /// List blobs in a container
        /// </summary>
        /// <typeparam name="T">The type by which to filter results; use <see cref="IListBlobItem"/> to return all </typeparam>
        /// <param name="container">from which to list blobs</param>
        /// <param name="prefix">This represents one or more virtual directories (e.g. "\folder1\folder2")</param>
        /// <returns>An enumerable of the given type</returns>
        public static async Task<IEnumerable<T>> ListBlobs<T>(this CloudBlobContainer container, string prefix = "") where T : class, IListBlobItem
        {
            List<T> results = new List<T>();
            BlobContinuationToken continuationToken = null;

            do
            {
                BlobResultSegment resultSegment = await container.ListBlobsSegmentedAsync(prefix,
                    false, BlobListingDetails.Metadata, null, continuationToken, null, null);

                foreach (var blobItem in resultSegment.Results)
                {
                    // A hierarchical listing may return both virtual directories and blobs.
                    if (blobItem is T blob)
                        results.Add(blob);
                }

                // While ContinuationToken is not null, there are more segments to retrieve
                continuationToken = resultSegment.ContinuationToken;

            } while (continuationToken != null);

            return results;
        }
    }
}
