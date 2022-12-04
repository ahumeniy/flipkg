# flipkg: Flipper Package Manager 
## Build Server
Here's is the source code for the build server. It implements an Azure Function which can build a `.fap` file from a source repo.

# Requirements
- .NET Core SDK 6.0 or newer (https://dotnet.microsoft.com/es-es/download)
- Azure Functions Core Tools (https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=v4%2Cwindows%2Ccsharp%2Cportal%2Cbash#install-the-azure-functions-core-tools)

## Running the server
### Execute locally
Clone the repo, go to the `build-server` directory (this one), and execute `func start`.

The default URL should be http://localhost:7071, but listen to the above command output if it changes.

*You need to install the source code for your Flipper Zero firmware*. The default folder is `c:\repos\flipperzero-firmware`. You can change it at `local.settings.json` with the `FLIPPER_FIRMWARE_ROOT` variable.

### Run as Docker container
Clone the repo, go to the `build-server` directory (this one), and build the container with `docker build -t flipkg-build-server .`. You can replace the `flipkd-build-server` if you like.

Optionally, you can get a pre-built docker image with `docker pull ahumeniy/flipkg-build-server:latest`

Run the container with `docker run --rm -p 8080:80 flipkg-build-server`. You can make requests to `http://localhost:8080`

### Deploying to Azure
(Coming soon)

## Usage
Currently there is a single function defined which is `/api/Build`. It receives a `POST` request and expects the following body:

```
{
    "repoUrl": "repo-url",
    "branch": "branch",
    "subdirectory": "subdirectory"
}
```

Where:
- **repo-url**: Is the source repository for the application to build. It can be a repository containing an entire fork of the Flipper Zero firmware, but it can take longer to build.
- **branch**: The branch in the repository to clone. Can be null or omitted to clone the HEAD.
- **subdirectory**: Where to look in the source repo for the app source code. For repos originated from a fork of the Flipper Zero firmware. Can be null or omitted if the app is at the root of the repo.
- **applyPatch**: Optional patch to apply. Must be a gzipped GIT patch file encoded as base64 string.
