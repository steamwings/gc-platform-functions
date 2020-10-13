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
using System.Xml.Schema;

namespace PlatformFunctionsTests
{
    [TestClass]
    public class ImagesTests
    {
        private const string SampleGuid = "0f316fb1-db52-4790-b311-fe348a7a0cbe";
        private const string SamplePicPath = "samplePic";
        private static CloudBlobContainer PicsContainer;
        private static CloudBlobContainer PicsUploadsContainer;

        [ClassInitialize]
        public static async void ClassInitialize(TestContext context)
        {
            PicsContainer = TestHelper.CreateStorageContainer(context, StorageContainer.ProfilePics);
            PicsUploadsContainer = TestHelper.CreateStorageContainer(context, StorageContainer.ProfilePicsUploads);

            // Download sample pic
            using var http = new HttpClient();
            using var downloadStream = await http.GetStreamAsync("https://via.placeholder.com/600");
            using var fileStream = File.OpenWrite(SamplePicPath);
            downloadStream.CopyTo(fileStream);
        }

        /// <summary>
        /// Integration test to verify that medium and thumbnail images are created 
        /// and appropriately added when a profile picture is uploaded
        /// </summary>
        [DataRow("https://via.placeholder.com/600.gif")]
        [DataRow("https://via.placeholder.com/100.png")]
        [DataRow("https://via.placeholder.com/10000.jpg")]
        [DataTestMethod]
        public async Task CreatesMediumAndThumbnail(string samplePicUrl)
        {
            var blob = PicsUploadsContainer.GetBlockBlobReference(SampleGuid);

            using var http = new HttpClient();
            using var downloadStream = await http.GetStreamAsync(samplePicUrl);
            blob.UploadFromStream(downloadStream);

            Assert.IsTrue(blob.Exists());

            using (var largeStream = new MemoryStream())
            using (var mediumStream = new MemoryStream())
            using (var smallStream = new MemoryStream())
            {
                ProfilePictureUploaded.Run(blob, largeStream, mediumStream, smallStream, SampleGuid, NullLogger.Instance);
                Assert.That.IsSquare(Image.Load<Rgba32>(largeStream, out var formatL).Bounds(), Config.GetInt(ConfigKeys.ProfileWidthLarge));
                Assert.That.IsSquare(Image.Load<Rgba32>(mediumStream, out var formatM).Bounds(), Config.GetInt(ConfigKeys.ProfileWidthMedium));
                Assert.That.IsSquare(Image.Load<Rgba32>(smallStream, out var formatS).Bounds(), Config.GetInt(ConfigKeys.ProfileWidthSmall));
                Assert.IsInstanceOfType(formatL, typeof(PngFormat));
                Assert.IsInstanceOfType(formatM, typeof(PngFormat));
                Assert.IsInstanceOfType(formatS, typeof(PngFormat));
            }

            Assert.IsFalse(blob.Exists());
        }

        public void DeletesTextFile()
        {
            var blob = PicsUploadsContainer.GetBlockBlobReference(SampleGuid);
            blob.UploadText("Sample text");

            using var largeStream = new MemoryStream();
            using var mediumStream = new MemoryStream();
            using var smallStream = new MemoryStream();
            ProfilePictureUploaded.Run(blob, largeStream, mediumStream, smallStream, SampleGuid, NullLogger.Instance);

            Assert.AreEqual(0, largeStream.Position);
            Assert.AreEqual(0, mediumStream.Position);
            Assert.AreEqual(0, smallStream.Position);
            Assert.IsFalse(blob.Exists());
        }

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

            Assert.AreEqual(0, largeStream.Position);
            Assert.AreEqual(0, mediumStream.Position);
            Assert.AreEqual(0, smallStream.Position);
            Assert.IsFalse(blob.Exists());
        }
    }
}
