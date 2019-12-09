using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace RageLauncher
{
    public class Launcher
    {
        private readonly Config config;

        public Launcher()
        {
            config = new ConfigurationBuilder()
                .AddJsonFile("config.json", true, true)
                .Build()
                .Get<Config>();
        }

        public async Task Launch()
        {
            var sourcePath = config.SourcePath;
            var targetPath = config.TargetPath;

            Console.WriteLine("Source directory: " + sourcePath);
            Console.WriteLine("Target directory: " + targetPath);
            Console.WriteLine("Moving files...");

            var filesToMove = Directory.GetFiles(sourcePath).Select(p => Path.GetFileName(p));
            var directoriesToMove = Directory.GetDirectories(sourcePath).Select(p => Path.GetFileName(p));
            Move(sourcePath, targetPath, filesToMove, directoriesToMove);

            StartHookAndWaitForExit(targetPath);
            Console.WriteLine($"Waiting for {config.GameProcessName} to exit...");
            await WaitForGameToExit().ConfigureAwait(false);

            Console.WriteLine("Moving files back...");
            Move(targetPath, sourcePath, filesToMove, directoriesToMove);

            Console.WriteLine("All done, press Enter to exit.");
            Console.ReadLine();
        }

        private static void Move(string sourceDirectory, string targetDirectory, IEnumerable<string> fileNames, IEnumerable<string> directoryNames)
        {
            foreach (var fileName in fileNames)
            {
                var sourceFileName = Path.Combine(sourceDirectory, fileName);
                var targetFileName = Path.Combine(targetDirectory, fileName);
                File.Move(sourceFileName, targetFileName, overwrite: true);
            }

            foreach (var directoryName in directoryNames)
            {
                var sourceDirectoryName = Path.Combine(sourceDirectory, directoryName);
                var targetDirectoryName = Path.Combine(targetDirectory, directoryName);
                Directory.Move(sourceDirectoryName, targetDirectoryName);
            }
        }

        private void StartHookAndWaitForExit(string targetPath)
        {
            var ragePluginHookPath = Path.Combine(targetPath, config.RagePluginHookFileName);
            Console.WriteLine($"Starting " + ragePluginHookPath);

            using var process = new Process();
            process.StartInfo.FileName = ragePluginHookPath;
            process.StartInfo.WorkingDirectory = targetPath;
            process.Start();
            process.WaitForExit();

            Console.WriteLine($"The {config.RagePluginHookFileName} process has exited");
        }

        private async Task WaitForGameToExit()
        {
            var gameProcesses = Process.GetProcessesByName(config.GameProcessName);

            while (gameProcesses.Length > 0)
            {
                await Task.Delay(1000).ConfigureAwait(false);
                gameProcesses = Process.GetProcessesByName(config.GameProcessName);
            }

            // wait a bit more for file handles to be released
            await Task.Delay(5000).ConfigureAwait(false);
        }
    }
}