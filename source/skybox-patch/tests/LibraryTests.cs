using System;
using System.IO;
using System.Linq;
using SkyboxUsability;

class LibraryTests
{
    static int failures;
    static void Test(string name,Func<bool> check)
    {
        try {if(!check()) throw new Exception("assertion failed");Console.WriteLine("PASS "+name);}
        catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}
    }
    static int Main()
    {
        Test("extend range preserving original step",()=>{var a=LibraryPolicy.EyeValues(new[]{-.5f,-.4f,-.3f,-.2f,-.1f,0,.1f,.2f,.3f,.4f,.5f});return a.Count==21&&a.First()==-1&&a.Last()==1&&a[10]==0&&Math.Abs(a[11]-.1f)<.00001;});
        Test("fine eye step retained",()=>LibraryPolicy.EyeValues(new[]{-.5f,-.49f,0,.49f,.5f}).Count==201);
        Test("range expansion is idempotent",()=>{var a=LibraryPolicy.EyeValues(new[]{-.5f,0,.5f});return LibraryPolicy.EyeValues(a).SequenceEqual(a);});
        Test("only unfavourited videos selected",()=>LibraryPolicy.SelectNonFavorite(true,0)&&!LibraryPolicy.SelectNonFavorite(true,1)&&!LibraryPolicy.SelectNonFavorite(false,0));
        Test("known dimensions and MB",()=>LibraryPolicy.Details(7680,3840,512L*1024*1024)=="7680×3840 · 512 MB");
        Test("GB formatted without excessive decimals",()=>LibraryPolicy.Details(3840,2160,3L*1024*1024*1024/2)=="3840×2160 · 1.5 GB");
        Test("unknown metadata is explicit",()=>LibraryPolicy.Details(0,0,0)=="分辨率未知 · 大小未知");
        Test("sub-MB size not rounded to zero",()=>LibraryPolicy.Details(2,2,1024).Contains("<1 MB"));
        Test("only genuine EOF accepted",()=>LibraryPolicy.IsCompleted(1,false,true)&&!LibraryPolicy.IsCompleted(3,false,true)&&!LibraryPolicy.IsCompleted(5,true,true)&&!LibraryPolicy.IsCompleted(1,false,false));
        string dir=Path.Combine(Path.GetTempPath(),"skybox-library-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try
        {
            string file=Path.Combine(dir,"completed.json");
            Test("missing store is empty",()=>!new CompletedStore(file).Contains("a"));
            Test("completion survives a new instance",()=>{new CompletedStore(file).Mark("a");return new CompletedStore(file).Contains("a");});
            Test("marking does not erase other completions",()=>{var s=new CompletedStore(file);s.Mark("b");s.Mark("a");return new CompletedStore(file).Contains("a")&&new CompletedStore(file).Contains("b");});
            Test("completion does not mark sibling or unrelated file",()=>!new CompletedStore(file).Contains("ab"));
            Test("corrupt store is diagnosed",()=>{File.WriteAllText(file,"{bad");try{new CompletedStore(file).Contains("a");return false;}catch(System.Runtime.Serialization.SerializationException){return true;}});
        }
        finally {Directory.Delete(dir,true);}
        Console.WriteLine("Failures: "+failures);return failures==0?0:1;
    }
}
