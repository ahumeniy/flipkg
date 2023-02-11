FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /App

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0

RUN apt-get update && apt-get install python3 python3-pip wget git -y && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /App

ADD https://aka.ms/downloadazcopy-v10-linux .
RUN AZCP=`tar ztf downloadazcopy-v10-linux | grep /azcopy` && \
    tar -zxv --strip-components=1 -f downloadazcopy-v10-linux ${AZCP} && \
    mv azcopy /bin

COPY --from=build-env /App/out .

RUN chmod +x build-fap.sh

# Uncomment if creating container for an specific version of the firmware. Builds will be faster, but have to deploy a new container if version changes.
ENV FZBRANCH=dev
RUN git clone https://github.com/flipperdevices/flipperzero-firmware.git -b ${FZBRANCH} /App/flipper && \
    cd /App/flipper && \
    ./fbt

ENTRYPOINT ["bash", "build-fap.sh"]
#ENTRYPOINT /bin/bash
#ENTRYPOINT ["dotnet", "DotNet.Docker.dll"]