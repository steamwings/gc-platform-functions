using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Storage.Blob;

namespace PlatformFunctions
{
    public static class BackupEmailManual
    {
        [FunctionName("BackupEmailManual")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "backup-email")] HttpRequest req,
            [Blob("mail-backup/{DateTime.Now}-backup", FileAccess.ReadWrite)] CloudBlockBlob blob,
            ILogger log)
        {
            log.LogInformation($"{nameof(BackupEmailManual)} processing request...");

            await BackupEmail.DoBackup(blob, log);

            return new StatusCodeResult(200);
        }
    }
}
