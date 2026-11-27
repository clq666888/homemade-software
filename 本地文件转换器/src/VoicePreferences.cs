using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
namespace LocalConverter
{
    public sealed class VoicePreference{public string Alias="";public bool Favorite;}
    public sealed class VoicePreferences
    {
        readonly string file;Dictionary<string,VoicePreference> items=new Dictionary<string,VoicePreference>();
        public VoicePreferences(string path=null){file=path??Path.Combine(Settings.Folder,"voice-preferences.json");try{items=new JavaScriptSerializer().Deserialize<Dictionary<string,VoicePreference>>(File.ReadAllText(file))??items;}catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}}
        public VoicePreference Get(string pack,int id){VoicePreference value;return items.TryGetValue(pack+":"+id,out value)&&value!=null?value:new VoicePreference();}
        public void Set(string pack,int id,string alias,bool favorite){var next=new Dictionary<string,VoicePreference>(items);next[pack+":"+id]=new VoicePreference{Alias=(alias??"").Trim(),Favorite=favorite};Directory.CreateDirectory(Path.GetDirectoryName(file));string temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,new JavaScriptSerializer().Serialize(next));if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);items=next;}finally{OutputFiles.DeleteTemporary(temp);}}
    }
}
