using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;

namespace PlatformFunctionsTests
{
    [TestClass]
    public class TestHelper
    {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext testContext)
        {
            CopyTestContextToEnvironment(testContext);
        }

        protected static void CopyTestContextToEnvironment(TestContext testContext)
        {
            foreach (DictionaryEntry property in testContext.Properties)
            {
                if (property.Value is string value)
                {
                    Environment.SetEnvironmentVariable((string)property.Key, value);
                }
            }
        }

        public static CloudBlobContainer CreateStorageContainer(TestContext testContext, StorageContainer container)
        {
            var storageBlobEndpoint = (string)testContext.Properties["StorageBlobEndpoint"];
            var storageAccountName = (string)testContext.Properties["SharedUserStorageAccountName"];
            var storageKey = (string)testContext.Properties["SharedUserStorageKey"];

            var credentials = new StorageCredentials(storageAccountName, storageKey);
            var uri = new Uri(storageBlobEndpoint + '/' + storageAccountName + '/' + ContainerName(container) + '/');
            var blobContainer = new CloudBlobContainer(uri, credentials);

            blobContainer.CreateIfNotExists(BlobContainerPublicAccessType.Container);

            return blobContainer;
        }

        /// <summary>
        /// Get the actual name of the container
        /// </summary>
        /// <param name="container"></param>
        /// <returns>the name string</returns>
        /// <remarks>
        /// If simpler, this could be made programmatic by adding dashes and making lowercase.
        /// This could be made an extension method if moved to a static class.
        /// </remarks>
        private static string ContainerName(StorageContainer container)
            => container switch
            {
                StorageContainer.ProfilePicsUploads => "profile-pics-uploads",
                StorageContainer.ProfilePics => "profile-pics",
                _ => throw new NotImplementedException(),
            };
    }

    public enum StorageContainer
    {
        ProfilePics,
        ProfilePicsUploads
    }

}
