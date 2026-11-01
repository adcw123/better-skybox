using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
namespace SkyboxUsability
{
    public static class Policy
    {
        public static bool IsInFolder(string folder,string path)
        {
            if(string.IsNullOrWhiteSpace(folder)||string.IsNullOrWhiteSpace(path)) return false;
            string root=folder.Replace('\\','/').TrimEnd('/');
            string item=path.Replace('\\','/').TrimEnd('/');
            return string.Equals(root,item,StringComparison.OrdinalIgnoreCase)||item.StartsWith(root+"/",StringComparison.OrdinalIgnoreCase);
        }
        public static IEnumerable<int> RowIndices(int index,int columns,int count)
        {
            if(index<0 || index>=count || columns<1) yield break;
            int start=index/columns*columns;
            for(int i=start;i<Math.Min(start+columns,count);i++) yield return i;
        }
        public static bool ShouldSelect(IEnumerable<string> keys,Func<string,bool> selected)
        {
            var list=keys.Distinct().ToList();
            return list.Count>0 && list.Any(x=>!selected(x));
        }
        public static bool IsDirectChild(string folder,string file)
        {
            if(string.IsNullOrEmpty(folder)||string.IsNullOrEmpty(file)) return false;
            string normalized=file.Replace('\\','/').TrimEnd('/');
            int slash=normalized.LastIndexOf('/');
            return slash>=0 && string.Equals(folder.Replace('\\','/').TrimEnd('/'),normalized.Substring(0,slash),StringComparison.OrdinalIgnoreCase);
        }
        public static bool CanDeleteFolder(string folder)
        {
            if(string.IsNullOrWhiteSpace(folder)||!Path.IsPathRooted(folder)||!Directory.Exists(folder)) return false;
            string full=Path.GetFullPath(folder).TrimEnd('\\','/');
            if(full.Length==0 || string.Equals(full,Path.GetPathRoot(full).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase)) return false;
            return (File.GetAttributes(full)&FileAttributes.ReparsePoint)==0;
        }
    }
    public class FolderDeletion
    {
        bool busy,completed;
        public bool Execute(Func<bool> remove,Action close,Action cleanup,Action refresh)
        {
            if(busy||completed) return false;
            busy=true;
            try
            {
                if(!remove()) return false;
                completed=true;
                close();
                cleanup();
                refresh();
                return true;
            }
            finally {busy=false;}
        }
    }
    [DataContract]
    public class PlaybackDefaults
    {
        public static PlaybackDefaults ResetForFormat(int format)
        {
            if(format<0||format>22) throw new ArgumentOutOfRangeException("format");
            return new PlaybackDefaults{Format=format};
        }
        [DataMember] public int Version=1;
        [DataMember] public int Format=2;
        [DataMember] public int Speed=3;
        [DataMember] public int Ratio;
        [DataMember] public int Height;
        [DataMember] public float Rotate;
        [DataMember] public float Zoom;
        [DataMember] public float Tilt;
        [DataMember] public float Offset;
        [DataMember] public float Scale=1;
        [DataMember] public float Contrast=10;
        [DataMember] public float Luminance=10;
        [DataMember] public float Saturation=10;
        [DataMember] public bool FrameLock;
        [DataMember] public bool Curved;
        [DataMember] public bool Mono;
        [DataMember] public bool ExchangeEye;
        [DataMember] public bool FullSbs;
        public bool IsValid()
        {
            return Version==1 && Format>=0 && Format<=22 && Speed>=0 && Speed<=6 && Ratio>=0 && Ratio<=5 &&
                new[]{Rotate,Zoom,Tilt,Offset,Scale,Contrast,Luminance,Saturation}.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x)&&Math.Abs(x)<=10000) && Scale>0;
        }
    }
    public static class SettingsStore
    {
        public static PlaybackDefaults Load(string path)
        {
            if(!File.Exists(path)) return null;
            try { using(var f=File.OpenRead(path)) { var d=(PlaybackDefaults)new DataContractJsonSerializer(typeof(PlaybackDefaults)).ReadObject(f); return d!=null&&d.IsValid()?d:null; } }
            catch(SerializationException) { return null; }
        }
        public static void Save(string path,PlaybackDefaults value)
        {
            if(value==null||!value.IsValid()) throw new ArgumentException("Invalid playback defaults");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp=path+".tmp";
            using(var f=File.Create(temp)) new DataContractJsonSerializer(typeof(PlaybackDefaults)).WriteObject(f,value);
            if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
        }
        public static void Clear(string path) { if(File.Exists(path)) File.Delete(path); }
    }
    public static class FolderFormats
    {
        [DataContract] public class Entry
        {
            [DataMember] public string Folder;
            [DataMember] public int Format;
        }
        static List<Entry> Read(string path)
        {
            if(!File.Exists(path)) return new List<Entry>();
            using(var file=File.OpenRead(path)) return (List<Entry>)new DataContractJsonSerializer(typeof(List<Entry>)).ReadObject(file);
        }
        static void Write(string path,List<Entry> entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using(var file=File.Create(path+".tmp")) new DataContractJsonSerializer(typeof(List<Entry>)).WriteObject(file,entries);
            if(File.Exists(path)) File.Replace(path+".tmp",path,null); else File.Move(path+".tmp",path);
        }
        static string Normalize(string folder) { return folder.Replace('\\','/').TrimEnd('/'); }
        public static void Set(string path,string folder,int format)
        {
            if(format<0||format>22||string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("Invalid folder format");
            var entries=Read(path);
            entries.RemoveAll(x=>string.Equals(Normalize(x.Folder),Normalize(folder),StringComparison.OrdinalIgnoreCase));
            entries.Add(new Entry{Folder=folder,Format=format});
            Write(path,entries);
        }
        public static int? Find(string path,string file)
        {
            var entry=Read(path).LastOrDefault(x=>x.Format>=0&&x.Format<=22&&Policy.IsDirectChild(x.Folder,file));
            return entry==null?(int?)null:entry.Format;
        }
        public static void Remove(string path,string folder)
        {
            var entries=Read(path);
            entries.RemoveAll(x=>string.Equals(Normalize(x.Folder),Normalize(folder),StringComparison.OrdinalIgnoreCase));
            Write(path,entries);
        }
    }
}
