# syntax=docker/dockerfile:1

# Base images are pinned by digest next to their tag. Dependabot bumps both; to bump one by hand,
# take the digest of the multi-platform index (docker buildx imagetools inspect <image>:<tag>).

ARG FLUTTER_VERSION=3.47.8
ARG FFMPEG_VERSION=8.1.3
ARG FFMPEG_SHA256=7138d28c96d9d3e3af4ee3d8cad72741f8ffb40da90c1112235dea3ecd3178a3

FROM --platform=$BUILDPLATFORM debian:bookworm-slim@sha256:734a72b3d6142a71993b4bc580ca01d8ca687f77a2c422d96a961d3376b179bc AS flutter-sdk
ARG FLUTTER_VERSION
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl git unzip xz-utils \
    && rm -rf /var/lib/apt/lists/*
RUN git clone --depth 1 --branch ${FLUTTER_VERSION} https://github.com/flutter/flutter.git /opt/flutter
ENV PATH="/opt/flutter/bin:/opt/flutter/bin/cache/dart-sdk/bin:${PATH}"
RUN flutter config --no-analytics --no-cli-animations \
    && flutter precache --web

FROM flutter-sdk AS frontend
WORKDIR /src/frontend
COPY frontend/pubspec.yaml frontend/pubspec.lock ./
RUN flutter pub get --enforce-lockfile
COPY api/openapi.json /src/api/openapi.json
COPY frontend/ ./
RUN dart run swagger_parser \
    && dart run build_runner build \
    && flutter build web --release --no-web-resources-cdn

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e AS server
ARG TARGETARCH
WORKDIR /src/server
COPY server/nuget.config server/Directory.Build.props server/Directory.Packages.props server/.editorconfig ./
COPY server/Guardou.Domain/Guardou.Domain.csproj Guardou.Domain/
COPY server/Guardou.Application/Guardou.Application.csproj Guardou.Application/
COPY server/Guardou.Infrastructure/Guardou.Infrastructure.csproj Guardou.Infrastructure/
COPY server/Guardou.Api/Guardou.Api.csproj Guardou.Api/
COPY server/Guardou.Cli/Guardou.Cli.csproj Guardou.Cli/
RUN RID="linux-musl-$([ "$TARGETARCH" = "arm64" ] && echo arm64 || echo x64)" \
    && echo "$RID" > /tmp/rid \
    && dotnet restore Guardou.Cli/Guardou.Cli.csproj -r "$RID"
COPY server/ ./
RUN dotnet publish Guardou.Cli/Guardou.Cli.csproj -c Release -o /app --no-restore \
    -r "$(cat /tmp/rid)" --self-contained false \
    -p:UseAppHost=false -p:OpenApiGenerateDocuments=false

FROM alpine:3.24@sha256:f59abe656b692d901e184be3445e084786602108599d2a51d229c0f5a48c71bf AS ffmpeg
ARG FFMPEG_VERSION
ARG FFMPEG_SHA256
RUN apk add --no-cache build-base nasm pkgconf zlib-dev dav1d-dev curl
WORKDIR /src
RUN curl -fsSLo ffmpeg.tar.xz "https://ffmpeg.org/releases/ffmpeg-${FFMPEG_VERSION}.tar.xz" \
    && echo "${FFMPEG_SHA256}  ffmpeg.tar.xz" | sha256sum -c - \
    && tar -xJf ffmpeg.tar.xz --strip-components=1
RUN ./configure \
        --prefix=/opt/ffmpeg \
        --disable-debug --disable-doc --disable-network --disable-autodetect --disable-everything \
        --disable-ffplay --disable-avdevice \
        --enable-ffmpeg --enable-ffprobe --enable-zlib --enable-libdav1d \
        --enable-protocol=file,pipe \
        --enable-demuxer=mov,matroska,avi,mpegts,mpegps,asf,h264,hevc,m4v,mpegvideo,vc1 \
        --enable-parser=h263,h264,hevc,vp8,vp9,av1,mpeg4video,mpegvideo,vc1,mjpeg \
        --enable-decoder=h263,h264,hevc,vp8,vp9,libdav1d,mpeg4,mpeg1video,mpeg2video,msmpeg4v1,msmpeg4v2,msmpeg4v3,wmv1,wmv2,wmv3,vc1,mjpeg,prores \
        --enable-filter=scale,format,null,transpose,hflip,vflip \
        --enable-encoder=png \
        --enable-muxer=image2,image2pipe \
    && make -j"$(nproc)" \
    && make install

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:21c3f9798416cba4c0576a947c286231cb71ea2884896cb968253f5c9668f930
# The base image is pinned by digest and the layers are cached, so this layer would never pick
# up new package versions. CI passes a new value on every run to rebuild it.
ARG APK_UPGRADE_RUN
RUN apk upgrade --no-cache \
    && apk add --no-cache shadow su-exec libdav1d zlib poppler-utils fontconfig ttf-liberation libheif-tools \
    && addgroup -g 1000 guardou \
    && adduser -D -u 1000 -G guardou -h /home/guardou guardou \
    && mkdir -p /data \
    && chown guardou:guardou /data \
    && find / -xdev -perm /6000 -type f -exec chmod a-s {} +
COPY --chmod=755 docker/guardou.sh /usr/local/bin/guardou
COPY --chmod=755 docker/entrypoint.sh /usr/local/bin/entrypoint
COPY --from=ffmpeg /opt/ffmpeg/bin/ffmpeg /opt/ffmpeg/bin/ffprobe /usr/local/bin/
WORKDIR /app
COPY --from=server /app ./
COPY --from=frontend /src/frontend/build/web ./wwwroot/
ENV ASPNETCORE_HTTP_PORTS=2286
VOLUME ["/data"]
EXPOSE 2286
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:2286/health || exit 1
ENTRYPOINT ["entrypoint"]
CMD ["run"]
