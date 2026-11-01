using System;
using System.IO;
using SkyboxUsability;
class WorkflowTests
{
    static int errors;
    static void Check(string name,Func<bool> f){try{if(!f())throw new Exception("assertion failed");Console.WriteLine("PASS "+name);}catch(Exception e){errors++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
    static int Main()
    {
        Check("unopened is unwatched",()=>WorkflowPolicy.State(false,0,0)==WatchState.Unwatched);
        Check("opened but zeroed is partial",()=>WorkflowPolicy.State(false,0,1)==WatchState.Partial);
        Check("positive progress is partial",()=>WorkflowPolicy.State(false,2,0)==WatchState.Partial);
        Check("completion outranks resume",()=>WorkflowPolicy.State(true,5,1)==WatchState.Completed);
        Check("all filter includes completed",()=>WorkflowPolicy.Matches(WatchFilter.All,WatchState.Completed));
        Check("unwatched excludes partial",()=>!WorkflowPolicy.Matches(WatchFilter.Unwatched,WatchState.Partial));
        Check("next skips completed",()=>WorkflowPolicy.Next(0,4,false,i=>i==3)==3);
        Check("next wraps once",()=>WorkflowPolicy.Next(3,4,true,i=>i==1)==1);
        Check("all complete terminates",()=>WorkflowPolicy.Next(0,4,true,i=>false)==-1);
        Check("no-wrap does not go backwards",()=>WorkflowPolicy.Next(3,4,false,i=>true)==-1);
        Check("empty playlist terminates",()=>WorkflowPolicy.Next(0,0,true,i=>true)==-1);
        Check("last seconds retained",()=>WorkflowPolicy.Resume(598.8,600,false)==598);
        Check("opening seconds retained",()=>WorkflowPolicy.Resume(5.5,600,false)==5);
        Check("natural completion restarts at zero",()=>WorkflowPolicy.Resume(600,600,true)==0);
        Check("invalid timestamp rejected",()=>WorkflowPolicy.Resume(double.NaN,600,false)==0);
        Check("manual end position never opens beyond EOF",()=>WorkflowPolicy.Resume(999,600,false)==599);
        Check("short stick press triggers only on release",()=>{var d=new StickPress();return d.Update(true,1,"a")==StickAction.None&&d.Update(false,1.2,"a")==StickAction.Reset;});
        Check("long stick press triggers once without short action",()=>{var d=new StickPress();d.Update(true,1,"a");return d.Update(true,1.8,"a")==StickAction.Favorite&&d.Update(true,2,"a")==StickAction.None&&d.Update(false,2.1,"a")==StickAction.None;});
        Check("stick press does not cross videos",()=>{var d=new StickPress();d.Update(true,1,"a");return d.Update(false,1.1,"b")==StickAction.None;});
        Check("protected selection is removed, other selections retained",()=>{var d=new System.Collections.Generic.Dictionary<string,int>{{"favorite",1},{"ordinary",0}};int deselected=0;return WorkflowPolicy.RemoveProtected(d,(key,value)=>value==1,value=>deselected++)==1&&deselected==1&&d.Count==1&&d.ContainsKey("ordinary");});
        Check("all protected leaves an empty deletion set",()=>{var d=new System.Collections.Generic.Dictionary<string,int>{{"favorite",1}};WorkflowPolicy.RemoveProtected(d,(key,value)=>true,value=>{});return d.Count==0;});
        Check("favorites can be resolved from fresh metadata",()=>{var d=new System.Collections.Generic.Dictionary<string,int>{{"favorite",0}};WorkflowPolicy.RemoveProtected(d,(key,value)=>key=="favorite",value=>{});return d.Count==0;});
        Check("video defaults outrank folder and global",()=>ScopedDefaults.Resolve(new PlaybackDefaults{Zoom=3},new PlaybackDefaults{Zoom=2},new PlaybackDefaults{Zoom=1}).Zoom==3);
        Check("folder outranks global",()=>ScopedDefaults.Resolve(null,new PlaybackDefaults{Zoom=2},new PlaybackDefaults{Zoom=1}).Zoom==2);
        Check("global fallback",()=>ScopedDefaults.Resolve(null,null,new PlaybackDefaults{Zoom=1}).Zoom==1);
        Check("no override preserves original",()=>ScopedDefaults.Resolve(null,null,null)==null);
        string root=Path.Combine(Path.GetTempPath(),"skybox-scoped-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{
            string store=Path.Combine(root,"scoped.json");
            Check("folder saved for unopened siblings",()=>{ScopedDefaults.SaveFolder(store,@"D:\Videos\Set",new PlaybackDefaults{Zoom=2});return ScopedDefaults.FindFolder(store,@"D:\Videos\Set\new.mp4").Zoom==2;});
            Check("folder never leaks into child folders",()=>ScopedDefaults.FindFolder(store,@"D:\Videos\Set\sub\x.mp4")==null);
            Check("video record survives new read",()=>{ScopedDefaults.SaveVideo(store,@"D:\Videos\Set\x.mp4",new PlaybackDefaults{Offset=.3f});return ScopedDefaults.FindVideo(store,@"D:\Videos\Set\x.mp4").Offset==.3f;});
            Check("replacing folder preserves video override",()=>{ScopedDefaults.SaveFolder(store,@"D:\Videos\Set",new PlaybackDefaults{Zoom=4});return ScopedDefaults.FindVideo(store,@"D:\Videos\Set\x.mp4")!=null&&ScopedDefaults.FindFolder(store,@"D:\Videos\Set\y.mp4").Zoom==4;});
            Check("network URL parent preserved",()=>ScopedDefaults.Parent("smb://host/share/set/a.mp4")=="smb://host/share/set");
        }finally{Directory.Delete(root,true);}
        Console.WriteLine("Failures: "+errors);return errors==0?0:1;
    }
}
