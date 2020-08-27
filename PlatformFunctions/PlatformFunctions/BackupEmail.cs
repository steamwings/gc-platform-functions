using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using PlatformFunctions.Helpers;
using Renci.SshNet;

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
        public static void Run(
            [TimerTrigger("0 30 5 * * *", RunOnStartup = DoRunOnStartup)] TimerInfo myTimer, 
            ILogger log)
        {
            log.LogInformation($"{nameof(BackupEmail)} timer triggered at {DateTime.Now}. Next run scheduled at {myTimer.FormatNextOccurrences(1)}.");

            var username = Config.Get(ConfigKeys.MailServerUsername);
            var domain = Config.Get(ConfigKeys.MailServerDomain);
            var backupFolder = Config.Get(ConfigKeys.MailServerBackupFolderPath);

            if (log.CheckNull(LogLevel.Error, new { username, domain, backupFolder, ssh = Config.Get(ConfigKeys.MailServerSshKey) }, out _, "Missing configuration value."))
                return;

            try
            {
                using (var keyStream = Config.GetStream(ConfigKeys.MailServerSshKey))
                {
                    var connectionInfo = new ConnectionInfo(domain, username,
                            new PrivateKeyAuthenticationMethod(username, new PrivateKeyFile(keyStream)));
                    
                    using var client = new SftpClient(connectionInfo);
                    client.Connect();
                    if (!client.Exists(backupFolder))
                    {
                        log.LogError("No backup folder found!");
                        return;
                    }
                    foreach(var file in client.ListDirectory(backupFolder))
                    {

                    }
                }

            } catch (Exception e)
            {
                log.LogError(e, "oops");
            }
        }
    }
}
