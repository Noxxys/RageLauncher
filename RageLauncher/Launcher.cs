using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace RageLauncher
{
    public class Launcher
    {
        private readonly Config config;

        public Launcher()
        {
            config = new ConfigurationBuilder()
                .AddIniFile("config.ini", false, true)
                .Build()
                .Get<Config>();

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("RageLauncher.log")
                .CreateLogger();
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Prevent the command window from closing")]
        public async Task Launch()
        {
            var sourcePath = config.SourcePath;
            var targetPath = config.TargetPath;

            Log.Information("Source directory: {sourcePath}", sourcePath);
            Log.Information("Target directory: {targetPath}", targetPath);
            Log.Information("Moving files...");

            try
            {
                var filesToMove = Directory.GetFiles(sourcePath).Select(p => Path.GetFileName(p));
                var directoriesToMove = Directory.GetDirectories(sourcePath).Select(p => Path.GetFileName(p));
                Move(sourcePath, targetPath, filesToMove, directoriesToMove);

                StartHookAndWaitForExit(targetPath);
                Log.Information("Waiting for {gameProcessName} to exit...", config.GameProcessName);
                await WaitForGameToExit().ConfigureAwait(false);

                Log.Information("Moving files back...");
                Move(targetPath, sourcePath, filesToMove, directoriesToMove);
            }
            catch (Exception ex)
            {
                Log.Error("An error occurred. Please send the RageLauncher.log file to noxxys@gmail.com so I can help you with this problem. {ex}", ex);
            }

            Log.Information("Press Enter to exit.");
            Console.ReadLine();
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We don't want to interrupt the move because of a failure on one file")]
        private static void Move(string sourceDirectory, string targetDirectory, IEnumerable<string> fileNames, IEnumerable<string> directoryNames)
        {
            foreach (var fileName in fileNames)
            {
                var sourceFileName = Path.Combine(sourceDirectory, fileName);
                var targetFileName = Path.Combine(targetDirectory, fileName);

                try
                {
                    File.Move(sourceFileName, targetFileName, overwrite: true);
                }
                catch (Exception ex)
                {
                    Log.Error("Couldn't move file {source} to {destination} - {exception}", sourceFileName, targetFileName, ex);
                }
            }

            foreach (var directoryName in directoryNames)
            {
                var sourceDirectoryName = Path.Combine(sourceDirectory, directoryName);
                var targetDirectoryName = Path.Combine(targetDirectory, directoryName);

                try
                {
                    Directory.Move(sourceDirectoryName, targetDirectoryName);
                }
                catch (Exception ex)
                {
                    Log.Error("Couldn't move directory {source} to {destination} - {exception}", sourceDirectoryName, targetDirectoryName, ex);
                }
            }
        }

        private void StartHookAndWaitForExit(string targetPath)
        {
            var ragePluginHookPath = Path.Combine(targetPath, config.RagePluginHookFileName);
            Log.Information("Starting {ragePluginHookPath}", ragePluginHookPath);

            using var process = new Process();
            process.StartInfo.FileName = ragePluginHookPath;
            process.StartInfo.WorkingDirectory = targetPath;
            process.Start();
            process.WaitForExit();

            Log.Information("The {ragePluginHookFileName} process has exited", config.RagePluginHookFileName);
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