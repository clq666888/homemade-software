namespace LocalConverter
{
    public static class VideoCatalog
    {
        // Fixed upstream build: changing this requires testing codecs, filters and Chinese paths.
        public static readonly EnginePackage Runtime=new EnginePackage{
            Id="video-ffmpeg-20261125",Name="视频转换 / GIF 引擎",Folder="runtimes/video-ffmpeg-20261125",
            Url="https://github.com/FFmpeg-Builds/FFmpeg-Builds/releases/download/autobuild-2026-11-25-12-41/ffmpeg-N-129288-gc81e35cf2b-win64-gpl-shared.zip",
            Bytes=92372482,Sha256="cfbd8d1c44942ff93e213ff16f61118c1a982aa0e6a2b3187dec0cf31fdf5462",Archive="zip",License="GPL-3.0-or-later; licenses preserved in the upstream archive"};
    }
}
