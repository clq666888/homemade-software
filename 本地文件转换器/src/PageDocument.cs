using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LocalConverter
{
    public sealed class PageItem
    {
        public int Index;
        public int Rotation;
        public PageItem Clone() { return new PageItem { Index=Index, Rotation=Rotation }; }
    }
    public sealed class PageDocument
    {
        public string Source;
        public long OriginalLength;
        public DateTime OriginalModified;
        public List<PageItem> Pages = new List<PageItem>();
        readonly Stack<List<PageItem>> undo = new Stack<List<PageItem>>();
        public bool CanUndo { get { return undo.Count>0; } }
        public void Snapshot() { undo.Push(Pages.Select(p=>p.Clone()).ToList()); if(undo.Count>100) { var keep=undo.Take(100).Reverse().ToArray(); undo.Clear(); foreach(var x in keep)undo.Push(x); } }
        public void Undo() { if(CanUndo) Pages=undo.Pop(); }
        public void Delete(IEnumerable<int> indices)
        {
            var chosen=new HashSet<int>(indices); if(chosen.Count==0)return;
            if(Pages.Count-chosen.Count<1)throw new IOException("至少保留一页；如需清空，请移除整份文件。");
            Snapshot(); Pages=Pages.Where((p,i)=>!chosen.Contains(i)).ToList();
        }
        public void Rotate(IEnumerable<int> indices) { var chosen=indices.ToArray(); if(chosen.Length==0)return;Snapshot();foreach(int i in chosen)Pages[i].Rotation=(Pages[i].Rotation+90)%360; }
        public void Move(IEnumerable<int> indices,int before)
        {
            var chosen=new HashSet<int>(indices); if(chosen.Count==0)return; Snapshot();
            var moving=Pages.Where((p,i)=>chosen.Contains(i)).ToList(); int target=before-chosen.Count(i=>i<before);
            Pages=Pages.Where((p,i)=>!chosen.Contains(i)).ToList(); Pages.InsertRange(Math.Max(0,Math.Min(target,Pages.Count)),moving);
        }
        public static string Overwrite(string processed, string source, long length, DateTime modified)
        {
            var info=new FileInfo(source); if(info.Length!=length||info.LastWriteTimeUtc!=modified)throw new IOException("原文件在编辑期间已变化，请生成新文件以避免覆盖新内容。");
            string temp=Path.Combine(info.DirectoryName,".localconverter-"+Guid.NewGuid().ToString("N")+".tmp");
            string backup=source+".原件-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".bak";
            try { File.Copy(processed,temp,false); File.Replace(temp,source,backup); return backup; } finally { OutputFiles.DeleteTemporary(temp); }
        }
    }
}
