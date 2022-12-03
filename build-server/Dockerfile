FROM flipperdevices/flipperzero-toolchain:latest AS flipper-env

RUN git clone https://github.com/flipperdevices/flipperzero-firmware.git -b dev /usr/share/flipperzero-firmware && \
    cd /usr/share/flipperzero-firmware && \
    ./fbt

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS installer-env

# Build requires 3.1 SDK
COPY --from=mcr.microsoft.com/dotnet/core/sdk:3.1 /usr/share/dotnet /usr/share/dotnet

COPY . /src/dotnet-function-app
RUN cd /src/dotnet-function-app && \
    mkdir -p /home/site/wwwroot && \
    dotnet publish *.csproj --output /home/site/wwwroot

# To enable ssh & remote debugging on app service change the base image to the one below
# FROM mcr.microsoft.com/azure-functions/dotnet:4-appservice
FROM mcr.microsoft.com/azure-functions/dotnet:4
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    FLIPPER_FIRMWARE_ROOT=/usr/share/flipperzero-firmware \
    FLIPPER_BUILD_BRANCH=dev \
    FBT_NO_SYNC=1

RUN apt-get update && apt-get install python3 python3-pip wget git -y && \
    rm -rf /var/lib/apt/lists/*

COPY --from=flipper-env ["/usr/share/flipperzero-firmware", "/usr/share/flipperzero-firmware"]

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]