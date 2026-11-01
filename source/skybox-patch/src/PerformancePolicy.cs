using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
namespace SkyboxUsability
{
    public static class ViewScratch
    {
        sealed class Buffers
        {
            internal readonly List<string> Keys=new List<string>();
            internal readonly HashSet<string> Visible=new HashSet<string>();
        }
        static readonly ConditionalWeakTable<object,Buffers> buffers=new ConditionalWeakTable<object,Buffers>();
        public static List<string> Keys(object view){var list=buffers.GetOrCreateValue(view).Keys;list.Clear();return list;}
        public static HashSet<string> Visible(object view){var set=buffers.GetOrCreateValue(view).Visible;set.Clear();return set;}
    }
    public sealed class RevisionStamp
    {
        int first,second;
        bool initialized;
        public bool Changed(int a,int b)
        {
            if(initialized&&first==a&&second==b)return false;
            initialized=true;first=a;second=b;return true;
        }
        public void Reset(){initialized=false;}
    }
    public sealed class SelectionIndex
    {
        object owner;
        int revision;
        readonly Dictionary<string,int> indices=new Dictionary<string,int>(StringComparer.Ordinal);
        public int Find(object list,int version,Func<IEnumerable<string>> keys,string path)
        {
            if(!object.ReferenceEquals(list,owner)||version!=revision)
            {
                indices.Clear();int i=0;
                foreach(string key in keys()){if(key!=null&&!indices.ContainsKey(key))indices.Add(key,i);i++;}
                owner=list;revision=version;
            }
            int value;return path!=null&&indices.TryGetValue(path,out value)?value:-1;
        }
    }
}
