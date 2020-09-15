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
        public static bool LastRunSucceeded = false;

        /// <summary>
        /// Use SSH, SFTP, Blob storage to create and backup email archive 
        /// </summary>
        /// <param name="myTimer">Timer that triggers this function--may be null when this is manually triggered</param>
        /// <param name="blob">New Blob to use for backup</param>
        /// <param name="log"></param>
        [FunctionName(nameof(BackupEmail))]
        public static async Task Run(
            [TimerTrigger("0 30 5 * * *", RunOnStartup = DoRunOnStartup)] TimerInfo myTimer,
            [Blob("mail-backup/{DateTime.Now}-backup", FileAccess.ReadWrite)] CloudBlockBlob blob,
            ILogger log)
        {
            log.LogInformation($"{nameof(BackupEmail)} timer triggered at {DateTime.Now}. Next run scheduled at {myTimer?.FormatNextOccurrences(1)}.");
            
            await DoBackup(blob, log);
            
            return;
        }

        public static async Task<bool> DoBackup(CloudBlockBlob blob, ILogger log)
        {
            var username = Config.Get(ConfigKeys.MailServerUsername);
            var domain = Config.Get(ConfigKeys.MailServerDomain);
            var backupFolder = Config.Get(ConfigKeys.MailServerBackupFolderPath);
            var archiveFilename = Config.Get(ConfigKeys.MailServerBackupArchiveFilename);

            log.LogDebug("Performing mail backup.");

            if (log.CheckNull(LogLevel.Error, new { username, domain, backupFolder, archiveFilename, ssh = Config.Get(ConfigKeys.MailServerSshKey) }, out _,
                "Missing configuration value."))
                return false;
            
            try
            {
                ConnectionInfo connectionInfo;
                using (var keyStream = Config.GetStream(ConfigKeys.MailServerSshKey))
                {
                    connectionInfo = new ConnectionInfo(domain, username,
                            new PrivateKeyAuthenticationMethod(username, new PrivateKeyFile(keyStream)));
                }

                // Connect with SSH
                using var ssh = new SshClient(connectionInfo);
                ssh.Connect();

                if (!TrySshCommand(log, "Create archive file",
                    ssh.RunCommand($"sudo tar -cvf {archiveFilename} {backupFolder}")))
                    return false;

                // Connect with SFTP
                using var sftp = new SftpClient(connectionInfo);
                sftp.Connect();

                if (!sftp.Exists(archiveFilename))
                {
                    log.LogError("The backup archive was not found!");
                    return false;
                }

                var stream = sftp.OpenRead(archiveFilename);
                await blob.UploadFromStreamAsync(stream);
                stream.Dispose();

                ssh.RunCommand($"rm -f {archiveFilename}");

                // If successful, delete old backup files
                foreach (var b in await blob.Container.ListBlobs<CloudBlockBlob>())
                {
                    // Delete when > than 2 days old; generally keeps a couple files just in case
                    if (b.Properties.Created < DateTimeOffset.Now.AddDays(-2))
                        b.Delete();
                }

                return true;
            }
            catch (Exception e)
            {
                log.LogError(e, "An exception occurred while attempting an email backup!");
            }
            return false;
        }

        /// <summary>
        /// Perform an <see cref="SshCommand"/> with appropriate logging
        /// </summary>
        /// <param name="log">to log with</param>
        /// <param name="description">of what the command is doing (e.g. "Create archive file")</param>
        /// <param name="command">to try</param>
        /// <returns></returns>
        private static bool TrySshCommand(ILogger log, string description, SshCommand command)
        {
            bool success = command.ExitStatus == 0;
            if (success)
            {
                log.LogDebug($"Command succeeded: '{description}'");
            } else
            {
                log.LogError("Command failed: '{0}'. Exit Status: '{1}', Result: '{2}', Error: '{3}' ", 
                    command.CommandText, command.ExitStatus, command.Result, command.Error);
            }
            log.LogTrace($"Full command run: ${command.CommandText}");
            return success;
        }
    }
}
