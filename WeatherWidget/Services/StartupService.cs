using System;
using System.Diagnostics;
using System.IO;

namespace WeatherWidget.Services
{
    public static class StartupService
    {
        private const string AppName = "WeatherWidget";

        public static bool IsEnabled()
        {
            try
            {
                var shortcutPath = GetShortcutPath();
                if (!File.Exists(shortcutPath))
                {
                    return false;
                }

                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    return false;
                }

                object? shellInstance = Activator.CreateInstance(shellType);
                if (shellInstance == null)
                {
                    return false;
                }

                dynamic shell = shellInstance;
                object? shortcutInstance = shell.CreateShortcut(shortcutPath);
                if (shortcutInstance == null)
                {
                    return false;
                }

                dynamic shortcut = shortcutInstance;
                string? target = shortcut.TargetPath as string;
                if (string.IsNullOrWhiteSpace(target))
                {
                    return false;
                }

                var exe = GetExecutablePath();
                try
                {
                    return string.Equals(Path.GetFullPath(target), Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return string.Equals(target, exe, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            var shortcutPath = GetShortcutPath();

            try
            {
                if (enabled)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Startup));

                    var shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType == null)
                    {
                        return;
                    }

                    object? shellInstance = Activator.CreateInstance(shellType);
                    if (shellInstance == null)
                    {
                        return;
                    }

                    dynamic shell = shellInstance;
                    object? shortcutInstance = shell.CreateShortcut(shortcutPath);
                    if (shortcutInstance == null)
                    {
                        return;
                    }

                    dynamic shortcut = shortcutInstance;
                    shortcut.TargetPath = GetExecutablePath();
                    shortcut.WorkingDirectory = AppContext.BaseDirectory;
                    shortcut.WindowStyle = 1;
                    shortcut.Description = AppName;
                    shortcut.Save();
                }
                else
                {
                    if (File.Exists(shortcutPath))
                    {
                        File.Delete(shortcutPath);
                    }
                }
            }
            catch
            {
                // Swallow errors to avoid crashes when startup folder isn't writable or COM unavailable.
            }
        }

        private static string GetShortcutPath()
        {
            var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            return Path.Combine(startup, $"{AppName}.lnk");
        }

        private static string GetExecutablePath()
        {
            string? executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                executablePath = Process.GetCurrentProcess().MainModule?.FileName
                    ?? Path.Combine(AppContext.BaseDirectory, "WeatherWidget.exe");
            }

            return executablePath;
        }
    }
}
