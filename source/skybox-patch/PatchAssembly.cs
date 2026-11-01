using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

class PatchAssembly
{
    const string OriginalHash="B6DAD50B364B271BE990F9254BC3EDBBD1CF1BC5455DBF11CB0AC311583C5130";
    static ModuleDefMD game,extension;
    static Importer importer;
    static MethodDef Method(string type,string method)
    {
        var t=game.Find("SkyboxVR."+type,false);
        if(t==null) throw new Exception("Missing game type "+type);
        var matches=t.Methods.Where(x=>x.Name==method).ToArray();
        if(matches.Length!=1) throw new Exception("Expected one game method: "+type+"::"+method);
        matches[0].Body.SimplifyBranches();
        return matches[0];
    }
    static Instruction Arg(MethodDef method,string name)
    {
        var p=name=="this"?method.Parameters.First(x=>x.IsHiddenThisParameter):method.Parameters.First(x=>x.Name==name);
        return Instruction.Create(OpCodes.Ldarg,p);
    }
    static IMethod Hook(string name)
    {
        var method=extension.Find("SkyboxUsability.Hooks",false).Methods.Single(x=>x.Name==name);
        // Importing a parameter TypeRef back into its defining assembly can produce
        // a ModuleRef to the extension. Mono then tries to load the DLL as a netmodule.
        // Bind game parameters directly to this module's TypeDefs instead.
        var parameters=method.MethodSig.Params.Select(type=>
        {
            var definition=game.Find(type.FullName,false);
            return definition!=null?definition.ToTypeSig():importer.Import(type);
        }).ToArray();
        TypeSig result=importer.Import(method.MethodSig.RetType);
        var scope=new AssemblyRefUser(extension.Assembly);
        var declaring=new TypeRefUser(game,"SkyboxUsability","Hooks",scope);
        return new MemberRefUser(game,name,MethodSig.CreateStatic(result,parameters),declaring);
    }
    static void AtReturns(MethodDef method,string hook,params string[] args)
    {
        var returns=method.Body.Instructions.Where(x=>x.OpCode==OpCodes.Ret).ToArray();
        foreach(var ret in returns)
        {
            int offset=method.Body.Instructions.IndexOf(ret)+1;
            ret.OpCode=OpCodes.Nop;
            ret.Operand=null;
            foreach(string arg in args) method.Body.Instructions.Insert(offset++,Arg(method,arg));
            method.Body.Instructions.Insert(offset++,Instruction.Create(OpCodes.Call,Hook(hook)));
            method.Body.Instructions.Insert(offset,Instruction.Create(OpCodes.Ret));
        }
    }
    static void Before(MethodDef method,string hook,params string[] args)
    {
        int i=0;
        foreach(string arg in args) method.Body.Instructions.Insert(i++,Arg(method,arg));
        method.Body.Instructions.Insert(i,Instruction.Create(OpCodes.Call,Hook(hook)));
    }
    static void SkipWhenTrue(MethodDef method,string hook,params string[] args)
    {
        var first=method.Body.Instructions[0];int offset=0;
        foreach(string arg in args)method.Body.Instructions.Insert(offset++,Arg(method,arg));
        method.Body.Instructions.Insert(offset++,Instruction.Create(OpCodes.Call,Hook(hook)));
        method.Body.Instructions.Insert(offset++,Instruction.Create(OpCodes.Brfalse,first));
        method.Body.Instructions.Insert(offset,Instruction.Create(OpCodes.Ret));
    }
    static bool IsFileView(TypeDef type)
    {
        for(var current=type;current!=null;current=current.BaseType==null?null:game.Find(current.BaseType.FullName,false))
            if(current.FullName=="SkyboxVR.FileScrollView")return true;
        return false;
    }
    static int Main(string[] args)
    {
        try
        {
            if(args.Length!=3) throw new ArgumentException("Usage: PatchAssembly original.dll extension.dll output.dll");
            using(var sha=SHA256.Create())
            using(var f=File.OpenRead(args[0]))
                if(BitConverter.ToString(sha.ComputeHash(f)).Replace("-","")!=OriginalHash) throw new Exception("Original SKYBOX assembly hash mismatch; refusing to patch another version");
            game=ModuleDefMD.Load(args[0]);extension=ModuleDefMD.Load(args[1]);importer=new Importer(game);
            AtReturns(Method("MainProcedure","Start"),"Ensure");
            AtReturns(Method("DeleteFileScript","RefreshPanelState"),"DeleteUi","this","deleteButton");
            AtReturns(Method("AdvancedPageManager","Start"),"SettingsUi","this");
            foreach(string method in new[]{"FillVideoInfoToItem","FillPictureInfoToItem","FillDirectoryInfoToItem"})
            {
                Before(Method("FileScrollView",method),"ReleaseCell","fileItemEvent");
                AtReturns(Method("FileScrollView",method),"BindCell","fileItemEvent");
            }
            var click=Method("FileItemEvent","OnButtonClicked");
            var first=click.Body.Instructions[0];
            click.Body.Instructions.Insert(0,Arg(click,"this"));
            click.Body.Instructions.Insert(1,Instruction.Create(OpCodes.Call,Hook("SkipFolderClick")));
            click.Body.Instructions.Insert(2,Instruction.Create(OpCodes.Brfalse,first));
            click.Body.Instructions.Insert(3,Instruction.Create(OpCodes.Ret));
            var load=Method("VideoTextureManager","LoadVideoFile");
            Before(load,"BeforeVideo","videoFileInfo");
            AtReturns(load,"AfterVideo","this","videoFileInfo");
            Before(Method("BaseSliderControlScript","Awake"),"EyeSlider","this");
            Before(Method("VideoPlayerCtrl","HandleVideoLooping"),"VideoEnded","this","hasError");
            AtReturns(Method("VideoPlayerCtrl","HandleVideoLooping"),"EndHandling");
            SkipWhenTrue(Method("VideoFileManager","PlayNextVideoFile"),"AutoNext","this");
            SkipWhenTrue(Method("VideoFileManager","set_CurrentDeleteFileState"),"DeleteGate","this","value");
            AtReturns(Method("FileScrollView","Awake"),"BrowseUi","this");
            AtReturns(Method("FavoriteButtonScript","Awake"),"FavoriteReady","this");
            // Rebuild/filter after any base or derived mutation, keeping count,
            // virtualization and bulk selection on the same filtered list.
            foreach(var type in game.GetTypes().Where(IsFileView))
            foreach(var method in type.Methods.Where(x=>x.HasBody&&new[]{"SetNeedShowList","SetCurrentFileList","SetList","UpdateList","DealUpdateInfo","InsertIntoCurrentList","UpdateCurrentList"}.Contains(x.Name.String)))
            {method.Body.SimplifyBranches();AtReturns(method,"FilterList","this");}
            var save=Method("VideoPlayerCtrl","SaveVideoFile");
            var duration=save.Body.Instructions.First(x=>x.Operand is IMethod&&((IMethod)x.Operand).Name=="get_Duration");
            int saveIndex=save.Body.Instructions.IndexOf(duration)-1;
            var anchor=save.Body.Instructions[saveIndex];
            if(anchor.OpCode!=OpCodes.Ldarg_0)throw new Exception("Unexpected save-progress IL anchor");
            anchor.OpCode=OpCodes.Nop;
            save.Body.Instructions.Insert(++saveIndex,Arg(save,"this"));
            var update=Method("FileScrollView","Update");
            int buffers=0;
            foreach(var instruction in update.Body.Instructions.Where(x=>x.OpCode==OpCodes.Newobj).ToArray())
            {
                var constructor=instruction.Operand as IMethod;
                string typeName=constructor==null?"":constructor.DeclaringType.FullName;
                string hook=typeName=="System.Collections.Generic.List`1<System.String>"?"ScratchKeys":typeName=="System.Collections.Generic.HashSet`1<System.String>"?"ScratchVisible":null;
                if(hook==null)continue;
                instruction.OpCode=OpCodes.Ldarg_0;instruction.Operand=null;
                update.Body.Instructions.Insert(update.Body.Instructions.IndexOf(instruction)+1,Instruction.Create(OpCodes.Call,Hook(hook)));
                buffers++;
            }
            if(buffers!=2)throw new Exception("Expected exactly two per-frame view scratch allocations");
            save.Body.Instructions.Insert(++saveIndex,Instruction.Create(OpCodes.Call,Hook("PreserveProgress")));
            save.Body.Instructions.Insert(++saveIndex,Arg(save,"this"));
            foreach(var type in game.GetTypes()) foreach(var method in type.Methods)
                if(method.HasBody) method.Body.OptimizeBranches();
            game.Write(args[2]);
            Console.WriteLine("Patched verified original to "+args[2]);
            return 0;
        }
        catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
}
