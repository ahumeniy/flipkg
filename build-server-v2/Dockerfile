FROM mcr.microsoft.com/dotnet/aspnet:6.0

RUN apt-get update && apt-get install python3 python3-pip wget git -y && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /App

# Copy the script and make it executable
COPY . /App
RUN chmod +x build-fap.sh

# Add AZCopy
ADD https://aka.ms/downloadazcopy-v10-linux .
RUN AZCP=`tar ztf downloadazcopy-v10-linux | grep /azcopy` && \
    tar -zxv --strip-components=1 -f downloadazcopy-v10-linux ${AZCP} && \
    mv azcopy /bin && \
    rm downloadazcopy-v10-linux

# Uncomment if creating container for an specific version of the firmware. Builds will be faster, but have to deploy a new container if version changes.
# ENV FZBRANCH=0.77.1
# RUN git clone https://github.com/flipperdevices/flipperzero-firmware.git -b ${FZBRANCH} /App/flipper && \
#     cd /App/flipper && \
#     ./fbt

ENTRYPOINT ["bash", "build-fap.sh"]