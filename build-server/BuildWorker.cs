using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace build_server
{
    public class BuildException : Exception
    {
        public BuildException(string message, int statusCode): base (message)
        {
            this.StatusCode = statusCode;
        }

        public BuildException(string message, int statusCode, Exception ex): base(message, ex)
        {
            this.StatusCode = statusCode;
        }

        public int StatusCode { get; set; }
    }

    public class BuildWorker
    {
        private readonly string firmwareRoot = null;
        private readonly ILogger log;

        public BuildWorker(ExecutionContext context, ILogger log)
        {
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            this.log = log;
            this.firmwareRoot = configurationBuilder["FLIPPER_FIRMWARE_ROOT"];
        }

        public async Task<BuildResult> Build(BuildRequest order)
        {
            // Clone repo
            string destinationPath = await CloneRepo(order);

            // Locate manifest
            string manifestFile = string.Empty;

            foreach (var file in new DirectoryInfo(destinationPath).GetFiles())
            {
                if (file.Name.Equals("application.fam", StringComparison.CurrentCultureIgnoreCase))
                {
                    manifestFile = file.FullName;
                }
            }

            if (string.IsNullOrEmpty(manifestFile))
            {
                log.LogInformation("No manifest found at {0}", destinationPath);
                throw new BuildException("No manifest found.", StatusCodes.Status400BadRequest);
            }

            // Read manifest
            var (manifestData, manifestContent) = await ParseManifest(order, manifestFile);

            // Validating manifest
            if (manifestData == null)
            {
                log.LogError("Manifest data is null.");
                throw new BuildException("Error reading manifest data", StatusCodes.Status500InternalServerError);
            }

            if (manifestData.Count == 0)
            {
                log.LogInformation("Manifest contains no apps: \n{0}", manifestContent);
                throw new BuildException("No apps found.", StatusCodes.Status404NotFound);
            }

            // Enumerating apps

            log.LogInformation("Apps found in manifest:");
            foreach (var appInfo in manifestData)
            {
                log.LogInformation("Application {0} type {1} category {2}", appInfo.AppId, appInfo.AppType, appInfo.Category);
            }

            // Building apps
            log.LogInformation("Building apps");

            var getFbtStartInfo = (string appid) => new ProcessStartInfo
            {
                FileName = Path.Combine(firmwareRoot, Environment.OSVersion.Platform == PlatformID.Win32NT ? "fbt.cmd" : "fbt"),
                WorkingDirectory = firmwareRoot,
                Arguments = $"fap_{appid}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            var outputFiles = new List<string>();
            var errorsFeed = new StringBuilder();

            foreach (var appInfo in manifestData.Where(x => x.AppType == "FlipperAppType.EXTERNAL" || x.AppType == "FlipperAppType.PLUGIN").ToArray())
            {
                if (appInfo.AppType == "FlipperAppType.PLUGIN") {
                    log.LogWarning("Detected FlipperAppType.PLUGIN. Might not build...");
                }

                log.LogInformation("Running fbt fap_{0}", appInfo.AppId);

                using (var fbtProcess = new Process() { StartInfo = getFbtStartInfo(appInfo.AppId) })
                {
                    fbtProcess.EnableRaisingEvents = true;

                    fbtProcess.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
                    {
                        if (e.Data == null) return;

                        log.LogInformation(e.Data);

                        if (e.Data.Trim().StartsWith("APPCHK"))
                        {
                            var outputFile = e.Data.Trim().Split("\t")[1];
                            log.LogInformation("fap generated at {0}", outputFile);
                            outputFiles.Add(outputFile);
                        }
                    };
                    fbtProcess.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
                    {
                        if (e.Data == null) return;

                        errorsFeed.AppendLine(e.Data);

                        log.LogError(e.Data);
                    };

                    fbtProcess.Start();

                    fbtProcess.BeginOutputReadLine();
                    fbtProcess.BeginErrorReadLine();

                    await fbtProcess.WaitForExitAsync();
                }
            }

            byte[] fileData = null;
            string fileDownloadName = string.Empty;
            var outputFaps = new List<FapOutputSpec>();
            var extappDir = Path.Combine(firmwareRoot, "build", "f7-firmware-D", ".extapps");

            // Retrieving binaries
            foreach (var output in outputFiles)
            {
                var outputFileName = Path.GetFileName(output);
                outputFaps.Add(new FapOutputSpec
                {
                    FileBytes = await File.ReadAllBytesAsync(Path.Combine(firmwareRoot, output)),
                    FileName = Path.GetFileName(output)
                });

                // Delete compiled files
                Directory.Delete(Path.Combine(extappDir, Path.GetFileNameWithoutExtension(outputFileName)), true);
                File.Delete(Path.Combine(extappDir, outputFileName));
                File.Delete(Path.Combine(extappDir, Path.GetFileNameWithoutExtension(outputFileName) + "_d.elf"));
            }

            // Packing binary(es)
            if (outputFaps.Count > 1)
            {
                using (var compressedFileStream = new MemoryStream())
                {
                    using (var zip = new ZipArchive(compressedFileStream, ZipArchiveMode.Create, false))
                    {
                        foreach (var output in outputFaps)
                        {
                            var entry = zip.CreateEntry(output.FileName);
                            using (var originalFileStream = new MemoryStream(output.FileBytes))
                            using (var zipEntryStream = entry.Open())
                            {
                                await originalFileStream.CopyToAsync(zipEntryStream);
                            }
                        }
                    }

                    fileData = compressedFileStream.ToArray();
                    fileDownloadName = manifestData.FirstOrDefault().Name + ".zip";
                }
            }
            else if (outputFaps.Count == 1)
            {
                fileData = outputFaps[0].FileBytes;
                fileDownloadName = outputFaps[0].FileName;
            }
            else
            {
                log.LogError("No faps generated.");
            }

            // Cleanup
            // Delete app source path
            try
            {
                Directory.Delete(destinationPath, true);
            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Couldn't delete {destinationPath}: {ex.Message}");
            }

            return new BuildResult()
            {
                HasFiles = outputFaps.Count > 0,
                FileData = fileData,
                FileName = fileDownloadName,
                Errors = errorsFeed.ToString()
            };
        }

        private async Task<string> CloneRepo(BuildRequest data)
        {
            var tempFolderName = Path.GetRandomFileName();
            var tempFolderPath = Path.Combine(Path.GetTempPath(), tempFolderName);

            var gitBranchParam = string.IsNullOrEmpty(data.Branch) ? "" : $"-b {data.Branch}";

            var gitStartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"clone {data.RepoUrl} {gitBranchParam} {tempFolderPath}",
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            try
            {
                log.LogInformation("Cloning repo {0} into {1}...", data.RepoUrl, tempFolderPath);

                using (var gitProcess = new Process() { StartInfo = gitStartInfo })
                {
                    gitProcess.Start();

                    await gitProcess.WaitForExitAsync();
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error opening git: " + ex.Message);
            }

            var destinationPath = Path.Combine(firmwareRoot, "applications_user", tempFolderName);

            var tempSubdirectory = tempFolderPath;

            if (!string.IsNullOrEmpty(data.Subdirectory))
            {
                tempSubdirectory = Path.Combine(tempFolderPath, data.Subdirectory);
            }

            log.LogInformation("Moving {0} into {1}", tempSubdirectory, destinationPath);

            Directory.Move(tempSubdirectory, destinationPath);

            try
            {
                // Delete temp directory if exists.
                if (Directory.Exists(tempFolderPath))
                {
                    Directory.Delete(tempFolderPath, true);
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, $"Couldn't delete {tempFolderPath}: {ex.Message}");
            }

            return destinationPath;
        }

        private async Task<(List<FlipperAppDefinition>, string)> ParseManifest(BuildRequest data, string manifestFile)
        {
            List<FlipperAppDefinition> manifestData = null;
            string manifestContent = string.Empty;

            // Remove trailing comma
            var trimComma = (string s) =>
            {
                s = s.Trim();

                if (!s.EndsWith(","))
                {
                    return s;
                }
                return s.Substring(0, s.Length - 1);
            };

            var unquote = (string s) =>
            {
                if (s.StartsWith("\"")) s = s.Substring(1, s.Length - 1);
                if (s.EndsWith("\"")) s = s.Substring(0, s.Length - 1);
                return s;
            };

            using (var manifestReader = new FileInfo(manifestFile).OpenText())
            {
                manifestContent = await manifestReader.ReadToEndAsync();

                log.LogInformation("manifest found: \n{0}", manifestContent);

                var patternApp = new Regex(@"(App\()([\sa-zA-Z0-9=""_,.*-\[\]]*)(\))");

                var appMatches = patternApp.Matches(manifestContent);

                foreach (Match appMatch in appMatches)
                {
                    if (appMatch.Success)
                    {
                        if (manifestData == null) manifestData = new List<FlipperAppDefinition>();

                        var appDefinition = new FlipperAppDefinition();

                        var appDataLines = appMatch.Groups[2].Value.Split('\n');

                        foreach (var line in appDataLines)
                        {
                            var parts = trimComma(line).Split('=');

                            switch (parts[0])
                            {
                                case "appid":
                                    appDefinition.AppId = unquote(parts[1]);
                                    break;
                                case "name":
                                    appDefinition.Name = unquote(parts[1]);
                                    break;
                                case "apptype":
                                    appDefinition.AppType = parts[1];
                                    break;
                                case "fap_category":
                                    appDefinition.Category = unquote(parts[1]);
                                    break;
                                default:
                                    break;
                            }
                        }

                        manifestData.Add(appDefinition);
                    }
                }
            }

            return (manifestData, manifestContent);
        }
    }
}