using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace build_server
{
    public static class FromQueue
    {
        [FunctionName("FromQueue")]
        public static async Task Run([QueueTrigger("build-orders")]BuildRequest request, 
        ILogger log, ExecutionContext context)
        {
            log.LogInformation($"Build order for: {request.RepoUrl} branch {request.Branch} folder {request.Subdirectory}");

            var worker = new BuildWorker(context, log);

            try
            {
                var output = await worker.Build(request);

                // var fileMime = Path.GetExtension(output.FileName) == ".zip" ? "application/zip" : "application/octet-stream";

                if (!output.HasFiles)
                {
                    log.LogWarning("No apps found.");
                    throw new BuildException("No apps found", 500);
                }

                var buildBranch = Environment.GetEnvironmentVariable("FLIPPER_BUILD_BRANCH");

                var getFileHash = (byte[] file) => {
                    SHA1 sha = SHA1.Create();
                    byte[] hash = sha.ComputeHash(file);
                    string res = Convert.ToHexString(hash);
                    return res;
                };

                var buildHash = getFileHash(output.FileData);

                var outputPath = $"{buildBranch}/{buildHash}/{output.FileName}";

                var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                var blobContainerClient = blobServiceClient.GetBlobContainerClient("fap-repo");
                var blobClient = blobContainerClient.GetBlobClient(outputPath);
                
                using (var ms = new MemoryStream(output.FileData))
                {
                    await blobClient.UploadAsync(ms);
                }

                log.LogInformation("App stored at {0}", outputPath);
            }
            catch (BuildException)
            {
                throw; // These already logged information to the default Logger, just throw.
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error building app");
                throw;
            }
        }
    }
}
