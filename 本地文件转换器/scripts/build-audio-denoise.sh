#!/usr/bin/env bash
# Run in an extracted FFmpeg 9.0.2 source directory with a MinGW-w64 toolchain on PATH.
set -euo pipefail
./configure --target-os=mingw32 --arch=x86_64 --enable-cross-compile \
  --cc=gcc --cxx=g++ --host-cc=gcc --pkg-config=false \
  --disable-everything --disable-autodetect --disable-doc --disable-debug \
  --disable-network --disable-avdevice --disable-swscale --disable-ffplay --disable-ffprobe \
  --disable-x86asm --disable-pthreads --enable-w32threads --enable-small \
  --enable-decoder=pcm_s16le --enable-encoder=pcm_s16le,wmav2 \
  --enable-demuxer=wav --enable-muxer=wav,asf --enable-protocol=file,pipe \
  --enable-filter=afftdn,bandreject,aresample,aformat,anull \
  --extra-ldflags=-static
mingw32-make -j4 ffmpeg.exe
strip ffmpeg.exe
