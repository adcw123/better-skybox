using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
namespace SkyboxUsability
{
    public static class ScopedDefaults
    {
        [DataContract] public sealed class Entry
        {
            [DataMember] public string Key;
            [DataMember] public bool Folder;
            [DataMember] public PlaybackDefaults Values;
        }
        static readonly object gate=new object();
        public static string Parent(string file)
        {
            if(string.IsNullOrEmpty(file)) return null;
            string p=file.Replace('\\','/').TrimEnd('/');int index=p.LastIndexOf('/');
            return index<0?null:p.Substring(0,index);
        }
        static string Normalize(string path) {return path.Replace('\\','/').TrimEnd('/');}
        static bool Same(string a,string b)
        {
            if(a==null||b==null) return false;
            bool windows=a.Length>1&&a[1]==':'||a.StartsWith("\\\\")||a.StartsWith("//");
            return string.Equals(Normalize(a),Normalize(b),windows?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);
        }
        static List<Entry> Read(string path)
        {
            if(!File.Exists(path))return new List<Entry>();
            using(var file=File.OpenRead(path))return (List<Entry>)new DataContractJsonSerializer(typeof(List<Entry>)).ReadObject(file)??new List<Entry>();
        }
        static void Save(string path,string key,bool folder,PlaybackDefaults values)
        {
            if(string.IsNullOrEmpty(key)||values==null||!values.IsValid())throw new ArgumentException("Invalid scoped playback settings");
            lock(gate)
            {
                var entries=Read(path);entries.RemoveAll(e=>e.Folder==folder&&Same(e.Key,key));
                entries.Add(new Entry{Key=key,Folder=folder,Values=values});
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                using(var file=File.Create(path+".tmp"))new DataContractJsonSerializer(typeof(List<Entry>)).WriteObject(file,entries);
                if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);
            }
        }
        public static void SaveFolder(string path,string folder,PlaybackDefaults values){Save(path,folder,true,values);}
        public static void SaveVideo(string path,string video,PlaybackDefaults values){Save(path,video,false,values);}
        public static PlaybackDefaults FindFolder(string path,string video)
        {
            lock(gate){var e=Read(path).LastOrDefault(x=>x.Folder&&Same(x.Key,Parent(video))&&x.Values!=null&&x.Values.IsValid());return e==null?null:e.Values;}
        }
        public static PlaybackDefaults FindVideo(string path,string video)
        {
            lock(gate){var e=Read(path).LastOrDefault(x=>!x.Folder&&Same(x.Key,video)&&x.Values!=null&&x.Values.IsValid());return e==null?null:e.Values;}
        }
        public static PlaybackDefaults Resolve(PlaybackDefaults video,PlaybackDefaults folder,PlaybackDefaults global){return video??folder??global;}
    }
}
