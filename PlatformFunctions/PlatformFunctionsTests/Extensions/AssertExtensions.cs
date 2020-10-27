using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace PlatformFunctionsTests.Extensions
{
    public static class AssertExtensions
    {
        /// <summary>
        /// Verify that one or more streams were not written to
        /// </summary>
        public static Assert StreamNotWritten(this Assert assert, params Stream[] streams)
        {
            foreach(var stream in streams)
            {
                if (stream.Position != 0 || stream.Length > 0)
                    throw new AssertFailedException($"Expected unwritten stream but got position,length=({stream.Position},{stream.Length})");
            }
            return assert;
        }
    }
}
