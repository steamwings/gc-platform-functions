using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Storage.Blob;
using PlatformFunctions.Helpers;
using Renci.SshNet;
using System.IO;

namespace PlatformFunctions
{
    /// <summary>
    /// Should back up email, contacts, calendars, and configuration
    /// </summary>
    public static class BackupEmail
    {
        const bool DoRunOnStartup
#if DEBUG
            = true; // This allows immediate trigger on local run
#else
            = false;
#endif

        [FunctionName(nameof(BackupEmail))]
        public static async Task Run(
            [TimerTrigger("0 30 5 * * *", RunOnStartup = DoRunOnStartup)] TimerInfo myTimer,
            [Blob("mail-backup")] CloudBlobContainer container, 
            ILogger log)
        {
            log.LogInformation($"{nameof(BackupEmail)} timer triggered at {DateTime.Now}. Next run scheduled at {myTimer.FormatNextOccurrences(1)}.");

            var username = Config.Get(ConfigKeys.MailServerUsername);
            var domain = Config.Get(ConfigKeys.MailServerDomain);
            var backupFolder = Config.Get(ConfigKeys.MailServerBackupFolderPath);

            if (log.CheckNull(LogLevel.Error, new { username, domain, backupFolder, ssh = Config.Get(ConfigKeys.MailServerSshKey) }, out _, "Missing configuration value."))
                return;

            await container.CreateIfNotExistsAsync();

            var virtualDirectoryPrefix = DateTime.Now.ToString("yyyymmdd");

            try
            {
                ConnectionInfo connectionInfo;
                using (var keyStream = Config.GetStream(ConfigKeys.MailServerSshKey))
                {
                    connectionInfo = new ConnectionInfo(domain, username,
                            new PrivateKeyAuthenticationMethod(username, new PrivateKeyFile(keyStream)));
                }

                // Connect to the server with SFTP
                using var client = new SftpClient(connectionInfo);
                client.Connect();
                if (!client.Exists(backupFolder))
                {
                    log.LogError("The backup folder was not found!");
                    return;
                }

                // Save block blobs to Azure blob storage
                foreach (var file in client.ListDirectory(backupFolder))
                {
                    if (!file.IsRegularFile) continue; // Skip any directories or links

                    var blockBlob = new CloudBlockBlob(new Uri($"{container.Uri}/{virtualDirectoryPrefix}/{file.Name}"));
                    
                    var stream = client.OpenRead(file.FullName);
                    await blockBlob.UploadFromStreamAsync(stream);
                    stream.Dispose();
                }

                // Delete old backup files, if any
                foreach (var directory in await container.ListBlobs<CloudBlobDirectory>())
                {
                    if (directory.Prefix == virtualDirectoryPrefix)
                        continue;

                    foreach (var blob in await container.ListBlobs<CloudBlockBlob>())
                    {
                        blob.Delete();
                    }
                }

            } catch (Exception e)
            {
                log.LogError(e, "An exception occurred while attempting an email backup!");
            }
        }
    }
}
