using System;
using System.IO;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PlatformFunctions.Images
{
    public static class ProfilePictureUploaded
    {
        /// <summary>
        /// Respond to an uploaded profile picture
        /// </summary>
        /// <param name="profilePic"></param>
        /// <param name="name"></param>
        /// <param name="log"></param>
        /// <remarks>
        /// TODO: Revoke SAS Url used (requires Active Directory)
        /// </remarks>
        [FunctionName(nameof(ProfilePictureUploaded))]
        public static void Run([BlobTrigger("profile-pics/{name}", Connection = "SharedUserStorage")] CloudBlockBlob profilePic,
            [Blob("profile-pics-medium/{name}", FileAccess.Write, Connection = "SharedUserStorage")] Stream medium,
            [Blob("profile-pics-thumbnails/{name}", FileAccess.Write, Connection = "SharedUserStorage")] Stream thumbnail,
            string name, ILogger log)
        {
            log.LogInformation($"${nameof(ProfilePictureUploaded)} processing blob (Name:{name} \n Size: {profilePic.Properties.Length} Bytes)");

            bool delete = false;
            using (var blobStream = profilePic.OpenRead())
            {
                try
                {
                    using var input = Image.Load<Rgba32>(blobStream, out IImageFormat format);
                    input.Mutate(x => x.Resize(300, 300));
                    input.Save(medium, format);
                    input.Mutate(x => x.Resize(100, 100));
                    input.Save(thumbnail, format);
                    log.LogTrace($"${nameof(ProfilePictureUploaded)} processing blob (Name:{name}, Format:{format.Name}");
                } 
                catch(Exception e) when (e is UnknownImageFormatException || e is InvalidImageContentException)
                {
                    log.LogWarning(e, $"${nameof(ProfilePictureUploaded)} read invalid profile picture '{name}'. The image will be deleted.");
                    delete = true;
                }
            }

            // Delete invalid images
            if (delete) profilePic.Delete();
        }
    }
}
