using System;
using System.IO;
using System.Linq;
using SkyboxUsability;

class PolicyTests
{
    static int failures;
    static void Check(string name, Func<bool> test)
    {
        try { if (!test()) throw new Exception("assertion failed"); Console.WriteLine("PASS " + name); }
        catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }
    static int Main()
    {
        Check("row includes folders as layout slots", () => Policy.RowIndices(5, 4, 10).SequenceEqual(new[] {4,5,6,7}));
        Check("last row is bounded", () => Policy.RowIndices(9, 4, 10).SequenceEqual(new[] {8,9}));
        Check("list view has one item per row", () => Policy.RowIndices(5, 1, 10).SequenceEqual(new[] {5}));
        Check("invalid row does not select anything", () => !Policy.RowIndices(-1, 4, 10).Any());
        Check("partial selection toggles to select all", () => Policy.ShouldSelect(new[] {"a","b"}, x => x == "a"));
        Check("complete selection toggles to clear all", () => !Policy.ShouldSelect(new[] {"a","b"}, x => true));
        Check("empty list does not toggle", () => !Policy.ShouldSelect(new string[0], x => false));
        Check("direct child matches", () => Policy.IsDirectChild(@"C:\Videos\Set", @"C:\Videos\Set\a.mp4"));
        Check("nested child excluded", () => !Policy.IsDirectChild(@"C:\Videos\Set", @"C:\Videos\Set\sub\a.mp4"));
        Check("prefix sibling excluded", () => !Policy.IsDirectChild(@"C:\Videos\Set", @"C:\Videos\Set2\a.mp4"));
        Check("path case and trailing slash normalized", () => Policy.IsDirectChild(@"C:\VIDEOS\Set\", @"c:\videos\set\a.mp4"));
        Check("network immediate child matches", () => Policy.IsDirectChild("smb://host/share/set", "smb://host/share/set/a.mp4"));
        Check("network nested child excluded", () => !Policy.IsDirectChild("smb://host/share/set", "smb://host/share/set/sub/a.mp4"));
        Check("deleted subtree includes exact folder",()=>Policy.IsInFolder(@"C:\Videos\Set",@"C:\Videos\Set"));
        Check("deleted subtree includes descendants",()=>Policy.IsInFolder(@"C:\Videos\Set",@"C:\Videos\Set\sub\a.mp4"));
        Check("deleted subtree excludes prefix sibling",()=>!Policy.IsInFolder(@"C:\Videos\Set",@"C:\Videos\Set2\a.mp4"));
        Check("successful deletion closes menu before refresh",()=>{var flow=new FolderDeletion();string order="";flow.Execute(()=>{order+="D";return true;},()=>order+="C",()=>order+="M",()=>order+="R");return order=="DCMR";});
        Check("refresh failure cannot repeat completed deletion",()=>{var flow=new FolderDeletion();int deletes=0;bool closed=false;try{flow.Execute(()=>{deletes++;return true;},()=>closed=true,()=>{},()=>{throw new IOException("refresh failed");});}catch(IOException){} flow.Execute(()=>{deletes++;return true;},()=>{},()=>{},()=>{});return deletes==1&&closed;});
        Check("failed shell deletion retains menu and allows retry",()=>{var flow=new FolderDeletion();bool closed=false;flow.Execute(()=>false,()=>closed=true,()=>{},()=>{});bool retry=flow.Execute(()=>true,()=>closed=true,()=>{},()=>{});return closed&&retry;});
        Check("drive root cannot be deleted", () => !Policy.CanDeleteFolder(@"C:\"));
        Check("relative folder cannot be deleted", () => !Policy.CanDeleteFolder("relative"));
        string dir = Path.Combine(Path.GetTempPath(), "skybox-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Check("ordinary absolute folder can be deleted", () => Policy.CanDeleteFolder(dir));
            string file = Path.Combine(dir, "defaults.json");
            Check("missing settings has no override", () => SettingsStore.Load(file) == null);
            Check("save survives a new read", () => { SettingsStore.Save(file, new PlaybackDefaults { Format = 3, Zoom = 1.5f, Tilt = 12, Speed = 5 }); var d=SettingsStore.Load(file); return d != null && d.Format==3 && d.Zoom==1.5f && d.Tilt==12 && d.Speed==5; });
            Check("saved settings exclude identity and progress", () => { string json=File.ReadAllText(file); return !json.Contains("PathOrUrl") && !json.Contains("LastProgressTime"); });
            Check("reset removes persistent override", () => {SettingsStore.Clear(file); return SettingsStore.Load(file)==null;});
            Check("corrupt settings do not load", () => {File.WriteAllText(file,"{bad"); return SettingsStore.Load(file)==null;});
            Check("invalid settings cannot replace good settings", () => {SettingsStore.Save(file,new PlaybackDefaults()); try { SettingsStore.Save(file,new PlaybackDefaults { Format=999 }); } catch (ArgumentException) {} return SettingsStore.Load(file).Format==2;});
            Check("factory defaults match original values", () => {var d=new PlaybackDefaults(); return d.Format==2 && d.Speed==3 && d.Scale==1 && d.Contrast==10 && d.Luminance==10 && d.Saturation==10 && d.Zoom==0 && d.Tilt==0 && !d.FrameLock && !d.Curved;});
            Check("reset preserves every supported current VR format",()=>Enumerable.Range(0,23).All(format=>PlaybackDefaults.ResetForFormat(format).Format==format));
            Check("reset restores viewing defaults while preserving format",()=>{var d=PlaybackDefaults.ResetForFormat(3);return d.Format==3&&d.Zoom==0&&d.Tilt==0&&d.Rotate==0&&d.Scale==1&&d.Speed==3&&d.Contrast==10&&d.Luminance==10&&d.Saturation==10;});
            Check("unsupported playback speed rejected",()=>!new PlaybackDefaults {Speed=7}.IsValid());
            string rules=Path.Combine(dir,"folders.json");
            Check("folder formats survive restart",()=>{FolderFormats.Set(rules,@"C:\Videos\Set",6);return FolderFormats.Find(rules,@"C:\Videos\Set\new.mp4")==6;});
            Check("folder formats never recurse",()=>FolderFormats.Find(rules,@"C:\Videos\Set\sub\a.mp4")==null);
            Check("folder format replacement",()=>{FolderFormats.Set(rules,@"C:\Videos\Set",3);return FolderFormats.Find(rules,@"C:\Videos\Set\a.mp4")==3;});
            Check("removing a folder removes its rule",()=>{FolderFormats.Remove(rules,@"C:\Videos\Set");return FolderFormats.Find(rules,@"C:\Videos\Set\a.mp4")==null;});
        }
        finally { Directory.Delete(dir, true); }
        Console.WriteLine("Failures: " + failures);
        return failures == 0 ? 0 : 1;
    }
}
