using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace LocalConverter
{
    public sealed class DownloadUpdate
    {
        public long Written,Total;
        public int FileIndex,FileCount,SavedFiles;
        public string FileName;
        public SavePhase Phase;
        public double BytesPerSecond;
        public bool AllSaved{get{return FileCount>0&&SavedFiles==FileCount;}}
        public int Percent{get{return AllSaved?100:Math.Min(99,(int)(100.0*Written/Math.Max(1,Total)));}}
    }
    public static class DownloadJob
    {
        public static string[] Save(string[] files,string folder,string name,CancellationToken token,Action<DownloadUpdate> progress)
        {
            long total=0,before=0;foreach(string file in files){token.ThrowIfCancellationRequested();total+=new FileInfo(file).Length;}
            var saved=new List<string>();var clock=Stopwatch.StartNew();
            for(int index=0;index<files.Length;index++)
            {
                token.ThrowIfCancellationRequested();string source=files[index];int current=index;
                long fileLength=new FileInfo(source).Length;
                string target=ResultSession.SaveCopy(source,folder,name??Path.GetFileNameWithoutExtension(source),token,p=>
                {
                    if(progress!=null)progress(new DownloadUpdate{Written=before+p.Written,Total=total,FileIndex=current+1,FileCount=files.Length,SavedFiles=current+(p.Phase==SavePhase.Saved?1:0),FileName=Path.GetFileName(source),Phase=p.Phase,BytesPerSecond=(before+p.Written)/Math.Max(0.001,clock.Elapsed.TotalSeconds)});
                });
                saved.Add(target);before+=fileLength;
            }
            return saved.ToArray();
        }
        public static string SizeText(double bytes)
        {
            string[] units={"B","KB","MB","GB","TB"};int unit=0;while(bytes>=1024&&unit<units.Length-1){bytes/=1024;unit++;}return bytes.ToString(unit==0?"0":"0.0")+" "+units[unit];
        }
    }
}
