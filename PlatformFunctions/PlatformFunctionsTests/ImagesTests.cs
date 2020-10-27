using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using PlatformFunctions.Helpers;
using PlatformFunctions.Images;
using PlatformFunctionsTests.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace PlatformFunctionsTests
{
    [TestClass]
    public class ImagesTests
    {
        private const string SampleGuid = "0f316fb1-db52-4790-b311-fe348a7a0cbe";
        private const string SamplePicPath = "samplePic";
        private static CloudBlobContainer PicsUploadsContainer;

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            PicsUploadsContainer = TestHelper.CreateStorageContainer(context, StorageContainer.ProfilePicsUploads);

            // Download sample pic
            using var http = new HttpClient();
            using var downloadStream = await http.GetStreamAsync("https://via.placeholder.com/600");
            using var fileStream = File.OpenWrite(SamplePicPath);
            downloadStream.CopyTo(fileStream);
        }

        [TestMethod]
        public void ScalesSamplePic()
        {
            var blob = PicsUploadsContainer.GetBlockBlobReference(SampleGuid);
            var bytes = File.ReadAllBytes(SamplePicPath);
            blob.UploadFromByteArray(bytes, 0, bytes.Length);

            using var largeStream = new MemoryStream();
            using var mediumStream = new MemoryStream();
            using var smallStream = new MemoryStream();
            ProfilePictureUploaded.Run(blob, largeStream, mediumStream, smallStream, SampleGuid, NullLogger.Instance);

            CheckSquarePng(largeStream, mediumStream, smallStream);
            Assert.IsFalse(blob.Exists());
        }

        /// <summary>
        /// Integration test to verify that scaled images are created 
        /// and appropriately added when a profile picture is uploaded
        /// </summary>
        [DataRow("https://via.placeholder.com/600")]
        [DataRow("https://via.placeholder.com/600.gif")]
        [DataRow("https://via.placeholder.com/100.png")]
        [DataRow("https://via.placeholder.com/1000.jpg")]
        [DataTestMethod]
        public async Task CreatesScaledImages(string samplePicUrl)
        {
            var blob = PicsUploadsContainer.GetBlockBlobReference(SampleGuid);

            using var http = new HttpClient();
            using var downloadStream = await http.GetStreamAsync(samplePicUrl);
            Assert.IsFalse(downloadStream.ToString().Contains("too big", System.StringComparison.OrdinalIgnoreCase));
            await blob.UploadFromStreamAsync(downloadStream);

            Assert.IsTrue(blob.Exists());

            using (var largeStream = new MemoryStream())
            using (var mediumStream = new MemoryStream())
            using (var smallStream = new MemoryStream())
            {
                ProfilePictureUploaded.Run(blob, largeStream, mediumStream, smallStream, SampleGuid, NullLogger.Instance);
                CheckSquarePng(largeStream, mediumStream, smallStream);
            }

            Assert.IsFalse(blob.Exists());
        }

        [TestMethod]
        public void IgnoresInvalidFilename()
        {
            var badId = "10f316fb";
            var blob = PicsUploadsContainer.GetBlockBlobReference(badId);
            var bytes = File.ReadAllBytes(SamplePicPath);
            blob.UploadFromByteArray(bytes, 0, bytes.Length);

            Assert.IsTrue(blob.Exists());

            using var largeStream = new MemoryStream();
            using var mediumStream = new MemoryStream();
            using var smallStream = new MemoryStream();
            ProfilePictureUploaded.Run(blob, largeStream, mediumStream, smallStream, badId, NullLogger.Instance);

            Assert.That.StreamNotWritten(smallStream, mediumStream, largeStream);
            Assert.IsFalse(blob.Exists());
        }

        [TestMethod]
        public void DeletesTextFile()
        {
            var blob = PicsUploadsContainer.GetBlockBlobReference(SampleGuid);
            blob.UploadText("Sample text");

            using var largeStream = new MemoryStream();
            using var mediumStream = new MemoryStream();
            using var smallStream = new MemoryStream();
            ProfilePictureUploaded.Run(blob, largeStream, mediumStream, smallStream, SampleGuid, NullLogger.Instance);

            Assert.That.StreamNotWritten(smallStream, mediumStream, largeStream);
            Assert.IsFalse(blob.Exists());
        }

        [TestMethod]
        public void DeletesInvalidPicture()
        {
            var blob = PicsUploadsContainer.GetBlockBlobReference(SampleGuid);
            var bytes = File.ReadAllBytes(SamplePicPath);
            bytes[1] = bytes[3] = 0xff; // Corrupt a little data
            blob.UploadFromByteArray(bytes, 0, bytes.Length);

            using var largeStream = new MemoryStream();
            using var mediumStream = new MemoryStream();
            using var smallStream = new MemoryStream();
            ProfilePictureUploaded.Run(blob, largeStream, mediumStream, smallStream, SampleGuid, NullLogger.Instance);

            Assert.That.StreamNotWritten(smallStream, mediumStream, largeStream);
            Assert.IsFalse(blob.Exists());
        }

        private void CheckSquarePng(Stream large, Stream medium, Stream small)
        {
            Assert.IsTrue(0 < large.Position);
            Assert.IsTrue(0 < medium.Position);
            Assert.IsTrue(0 < small.Position);
            large.Position = 0;
            medium.Position = 0;
            small.Position = 0;
            Assert.That.IsSquare(Image.Load<Rgba32>(large, out var formatL).Bounds(), Config.GetInt(ConfigKeys.ProfileWidthLarge));
            Assert.That.IsSquare(Image.Load<Rgba32>(medium, out var formatM).Bounds(), Config.GetInt(ConfigKeys.ProfileWidthMedium));
            Assert.That.IsSquare(Image.Load<Rgba32>(small, out var formatS).Bounds(), Config.GetInt(ConfigKeys.ProfileWidthSmall));
            Assert.IsInstanceOfType(formatL, typeof(PngFormat));
            Assert.IsInstanceOfType(formatM, typeof(PngFormat));
            Assert.IsInstanceOfType(formatS, typeof(PngFormat));
        }
    }
}
