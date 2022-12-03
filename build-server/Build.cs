using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace build_server
{
    public static class Build
    {
        [FunctionName("Build")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            BuildRequest data = JsonConvert.DeserializeObject<BuildRequest>(requestBody);

            log.LogInformation(requestBody);

            var worker = new BuildWorker(context, log);

            try
            {
                var output = await worker.Build(data);

                var fileMime = Path.GetExtension(output.FileName) == ".zip" ? "application/zip" : "application/octet-stream";

                if (output.HasFiles)
                    return new FileContentResult(output.FileData, fileMime) { FileDownloadName = output.FileName };
                else
                    return new ObjectResult($"Build error: \n{output.Errors.ToString()}") { StatusCode = StatusCodes.Status400BadRequest };
            }
            catch (BuildException ex)
            {
                return new ObjectResult(ex.Message) { StatusCode = ex.StatusCode };
            }
            catch (Exception ex)
            {
                return new ObjectResult(ex.Message) { StatusCode = StatusCodes.Status500InternalServerError };
            }
        }
    }
}
