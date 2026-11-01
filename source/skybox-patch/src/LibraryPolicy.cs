using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;

namespace SkyboxUsability
{
    public static class LibraryPolicy
    {
        public static List<float> EyeValues(IEnumerable<float> original)
        {
            var sorted=original.Where(x=>!float.IsNaN(x)&&!float.IsInfinity(x)).Distinct().OrderBy(x=>x).ToList();
            float step=1;
            for(int i=1;i<sorted.Count;i++) if(sorted[i]-sorted[i-1]>.0001f) step=Math.Min(step,sorted[i]-sorted[i-1]);
            int half=Math.Max(1,Math.Min(1000,(int)Math.Round(1.0/step)));
            return Enumerable.Range(-half,half*2+1).Select(i=>(float)Math.Round((double)i/half,5)).ToList();
        }
        public static bool SelectNonFavorite(bool video,long favoriteTime) {return video&&favoriteTime==0;}
        public static bool IsCompleted(long endReason,bool error,bool playing) {return endReason==1&&!error&&playing;}
        public static string Details(int width,int height,long bytes)
        {
            string resolution=width>0&&height>0?width+"×"+height:"分辨率未知";
            string size=bytes<=0?"大小未知":bytes<1024*1024?"<1 MB":bytes<1024L*1024*1024?
                (bytes/(1024.0*1024)).ToString("0.#",CultureInfo.InvariantCulture)+" MB":
                (bytes/(1024.0*1024*1024)).ToString("0.##",CultureInfo.InvariantCulture)+" GB";
            return resolution+" · "+size;
        }
    }
    public sealed class CompletedStore
    {
        readonly string path;
        HashSet<string> items;
        public CompletedStore(string path) {this.path=path;}
        void Load()
        {
            if(items!=null) return;
            var loaded=new List<string>();
            if(File.Exists(path)) using(var f=File.OpenRead(path)) loaded=(List<string>)new DataContractJsonSerializer(typeof(List<string>)).ReadObject(f);
            // Preserve URL case: some network servers distinguish paths by case.
            items=new HashSet<string>(loaded??new List<string>(),StringComparer.Ordinal);
        }
        public bool Contains(string key) {Load();return key!=null&&items.Contains(key);}
        public void Mark(string key)
        {
            if(string.IsNullOrEmpty(key)) return;
            Load();
            if(items.Contains(key)) return;
            var updated=new HashSet<string>(items,StringComparer.Ordinal);updated.Add(key);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp=path+".tmp";
            using(var f=File.Create(temp)) new DataContractJsonSerializer(typeof(List<string>)).WriteObject(f,updated.OrderBy(x=>x,StringComparer.Ordinal).ToList());
            if(File.Exists(path)) File.Replace(temp,path,null);else File.Move(temp,path);
            items=updated;
        }
    }
}
