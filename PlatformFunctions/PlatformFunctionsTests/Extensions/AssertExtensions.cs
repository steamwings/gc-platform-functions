using Microsoft.VisualStudio.TestTools.UnitTesting;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
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
    }
}
