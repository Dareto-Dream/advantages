FROM ubuntu:22.04

ARG DEBIAN_FRONTEND=noninteractive
ARG SERVER_BUILD_PATH=Builds/EdgegapServer

COPY ${SERVER_BUILD_PATH} /root/build/

WORKDIR /root/

RUN chmod +x /root/build/ServerBuild

# ca-certificates: TLS root store for outbound HTTPS/WSS calls to the gateway/relay.
# libgtk-3-0/libx11-6/libxrender1: the headless player still links against Unity's
# AppUI native plugin even with rendering stripped - without these the plugin fails
# to load (DllNotFoundException on Unity.AppUI.Core.Platform.Initialize). It's a
# caught exception rather than fatal, but fixing it keeps the startup log clean.
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
      ca-certificates libgtk-3-0 libx11-6 libxrender1 && \
    apt-get clean && \
    update-ca-certificates && \
    rm -rf /var/lib/apt/lists/*

CMD ["/bin/bash", "-c", "env;/root/build/ServerBuild -batchmode -nographics -job-worker-count 4 $UNITY_COMMANDLINE_ARGS"]
