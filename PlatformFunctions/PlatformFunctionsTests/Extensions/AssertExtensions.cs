using Microsoft.VisualStudio.TestTools.UnitTesting;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PlatformFunctionsTests.Extensions
{
    public static class AssertExtensions
    {
        public static Assert IsSquare(this Assert assert, Rectangle rectangle, double sideLength = 0)
        {
            if (rectangle.Height != rectangle.Width || (sideLength != 0 && rectangle.Width != sideLength))
                throw new AssertFailedException($"Rectangle had (h,w)=({rectangle.Height},{rectangle.Width}) but expected square"
                    + (sideLength != 0 ? "of size {sideLength}." : "."));
            return assert;
        }

        /// <summary>
        /// Verify that any number of streams were not written to
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
