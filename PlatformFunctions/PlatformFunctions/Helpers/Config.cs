using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PlatformFunctions.Helpers
{
    public enum ConfigKeys
    {
        DoPeriodicMailBackup,
        MailServerSshKey,
        MailServerUsername,
        MailServerDomain,
        MailServerBackupFolderPath,
        MailServerBackupArchiveFilename,
    }

    public static class Config
    {
        /// <summary>
        /// Wrapper method to get a configuration value.
        /// </summary>
        /// <param name="key">The <see cref="ConfigKeys"/> </param>
        /// <returns>string configuration value</returns>
        public static string Get(ConfigKeys key, string fallback = null)
        {
#if DEBUG
            // This is a workaround that's not exactly recommended but is much simpler than adding a bunch of startup configuration.
            // Right-click the project and select "Manage User Secrets" and add the appropriate keys
            if (Environment.GetEnvironmentVariable(key.ToString()) is null)
            {
                try
                {
                    var path = Environment.ExpandEnvironmentVariables(Environment.GetEnvironmentVariable("UserSecretsJsonPath"));
                    var jsonStream = File.OpenRead(path);
                    using var document = System.Text.Json.JsonDocument.Parse(jsonStream);
                    if (document.RootElement.TryGetProperty(key.ToString(), out var jsonElement)
                        && jsonElement.GetString() is string sshKey)
                    {
                        return sshKey;
                    }
                    throw new InvalidOperationException("User secrets are not configured properly.");
                } catch (Exception e)
                {
                    throw new InvalidOperationException("Configuration value not set. Did you forget to add it to local.settings.json? " +
                        "For keys that would be retrieved from Key Vault, add to User Secrets (right-click the project and select Manage User Secrets.)", e);
                }
            }
#endif
            return Environment.GetEnvironmentVariable(key.ToString()) ?? fallback;
        }

        public static bool GetBool(ConfigKeys key, bool fallback = false)
        {
            return bool.TryParse(Environment.GetEnvironmentVariable(key.ToString()), out bool flag)
                ? flag
                : fallback;
        }

        public static Stream GetStream(ConfigKeys key, Encoding encoding = null)
        {
            string value = Get(key);
            encoding ??= Encoding.UTF8;
            return new MemoryStream(encoding.GetBytes(value));
        }
    }
}
