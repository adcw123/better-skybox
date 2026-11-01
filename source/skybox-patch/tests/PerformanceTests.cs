using System;
using SkyboxUsability;
class PerformanceTests
{
    static int errors;
    static void Check(string name,Func<bool> test){try{if(!test())throw new Exception("assertion failed");Console.WriteLine("PASS "+name);}catch(Exception e){errors++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
    static int Main()
    {
        Check("scratch list reused and cleared per frame",()=>{var owner=new object();var a=ViewScratch.Keys(owner);a.Add("old");var b=ViewScratch.Keys(owner);return object.ReferenceEquals(a,b)&&b.Count==0;});
        Check("visible-set reused and cleared per frame",()=>{var owner=new object();var a=ViewScratch.Visible(owner);a.Add("old");var b=ViewScratch.Visible(owner);return object.ReferenceEquals(a,b)&&b.Count==0;});
        Check("views do not share scratch buffers",()=>!object.ReferenceEquals(ViewScratch.Keys(new object()),ViewScratch.Keys(new object())));
        Check("filter revisions avoid repeated work",()=>{var s=new RevisionStamp();return s.Changed(1,1)&&!s.Changed(1,1)&&s.Changed(1,2)&&!s.Changed(1,2)&&s.Changed(2,2);});
        Check("selection index reused until list revision changes",()=>{var index=new SelectionIndex();string[] keys={"a","b","c"};int enumerations=0;Func<System.Collections.Generic.IEnumerable<string>> source=()=>{enumerations++;return keys;};return index.Find(keys,1,source,"c")==2&&index.Find(keys,1,source,"b")==1&&enumerations==1;});
        Check("selection index follows reordering at unchanged count",()=>{var index=new SelectionIndex();var owner=new object();string[] keys={"a","b"};index.Find(owner,1,()=>keys,"a");keys=new[]{"b","a"};return index.Find(owner,2,()=>keys,"a")==1;});
        Console.WriteLine("Failures: "+errors);return errors==0?0:1;
    }
}
