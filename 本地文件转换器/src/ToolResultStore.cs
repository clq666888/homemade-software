using System;
using System.Collections.Generic;
using System.Linq;

namespace LocalConverter
{
    // Each tool owns its last batch. Switching tabs never exposes another tool's files.
    public sealed class ToolResultStore : IDisposable
    {
        sealed class Entry { public ResultSession Session; public string Context; }
        readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
        public ResultSession Get(string tool){Entry entry;return entries.TryGetValue(tool,out entry)?entry.Session:null;}
        public void Set(string tool,ResultSession session,string context)
        {
            Entry old;if(entries.TryGetValue(tool,out old)&&old.Session!=session)old.Session.Dispose();
            entries[tool]=new Entry{Session=session,Context=context};
        }
        public bool Matches(string tool,string context){Entry entry;return entries.TryGetValue(tool,out entry)&&entry.Context==context;}
        public void Invalidate(string tool){Entry entry;if(entries.TryGetValue(tool,out entry))entry.Context="stale-"+Guid.NewGuid().ToString("N");}
        public string[] PendingTools{get{return entries.Where(p=>!p.Value.Session.Downloaded&&p.Value.Session.Files.Length>0).Select(p=>p.Key).ToArray();}}
        public void Dispose(){foreach(var entry in entries.Values)entry.Session.Dispose();entries.Clear();}
    }
}
