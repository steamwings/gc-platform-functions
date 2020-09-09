using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Storage.Blob;
using PlatformFunctions.Helpers;
using Renci.SshNet;
using System.IO;
using Microsoft.Azure.Storage.Auth;

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

        /// <summary>
        /// Use SSH, SFTP, Blob storage to create and backup email archive 
        /// </summary>
        /// <param name="myTimer">Timer that triggers this function</param>
        /// <param name="blob">New Blob to use for backup</param>
        /// <param name="log"></param>
        [FunctionName(nameof(BackupEmail))]
        public static async Task Run(
            [TimerTrigger("0 30 5 * * *", RunOnStartup = DoRunOnStartup)] TimerInfo myTimer,
            [Blob("mail-backup/{DateTime.Now}-backup", FileAccess.ReadWrite)] CloudBlockBlob blob,
            ILogger log)
        {
            log.LogInformation($"{nameof(BackupEmail)} timer triggered at {DateTime.Now}. Next run scheduled at {myTimer.FormatNextOccurrences(1)}.");

            var username = Config.Get(ConfigKeys.MailServerUsername);
            var domain = Config.Get(ConfigKeys.MailServerDomain);
            var backupFolder = Config.Get(ConfigKeys.MailServerBackupFolderPath);
            var archiveFilename = Config.Get(ConfigKeys.MailServerBackupArchiveFilename);

            if (log.CheckNull(LogLevel.Error, new { username, domain, backupFolder, archiveFilename, ssh = Config.Get(ConfigKeys.MailServerSshKey) }, out _, 
                "Missing configuration value."))
                return;

            try
            {
                ConnectionInfo connectionInfo;
                using (var keyStream = Config.GetStream(ConfigKeys.MailServerSshKey))
                {
                    connectionInfo = new ConnectionInfo(domain, username,
                            new PrivateKeyAuthenticationMethod(username, new PrivateKeyFile(keyStream)));
                }

                using var ssh = new SshClient(connectionInfo);
                ssh.Connect();
                ssh.RunCommand($"sudo tar -cvf {archiveFilename} {backupFolder}");

                // Connect to the server with SFTP
                using var sftp = new SftpClient(connectionInfo);
                sftp.Connect();
                if (!sftp.Exists(archiveFilename))
                {
                    log.LogError("The backup archive was not found!");
                    return;
                }
                
                var stream = sftp.OpenRead(archiveFilename);
                await blob.UploadFromStreamAsync(stream);
                stream.Dispose();

                ssh.RunCommand($"rm -f {archiveFilename}");

                // If successful, delete old backup files
                foreach (var b in await blob.Container.ListBlobs<CloudBlockBlob>())
                {
                    // Delete when > than 2 days old; generally keeps a couple files just in case
                    if (b.Properties.Created < DateTimeOffset.Now.AddDays(2))
                        b.Delete();
                }

            } catch (Exception e)
            {
                log.LogError(e, "An exception occurred while attempting an email backup!");
            }
        }
    }
}
