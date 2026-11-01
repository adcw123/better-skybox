using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SkyboxVR;
using UnityEngine;
using UnityEngine.UI;

namespace SkyboxUsability
{
    public static class FolderEditor
    {
        static GameObject overlay;
        static RectTransform panel;
        static DirectoryFileInfo folder;
        static Text message;
        static ScrollViewType viewType;
        static FolderDeletion deletion;
        static int confirmedFavorites;
        public static void Open(FileItemEvent cell)
        {
            Close();
            folder=(DirectoryFileInfo)cell.FileInfo.DeepClone();
            deletion=new FolderDeletion();
            viewType=cell.CurScrollViewType;
            var view=cell.GetComponentInParent<FileScrollView>();
            Transform parent=view!=null?view.transform.parent:cell.GetComponentInParent<Canvas>().transform;
            var rect=NativeUi.Rect("SkyboxFolderOverlay",parent,new Vector2(1250,920),new Vector2(.5f,.5f),Vector2.zero);
            overlay=rect.gameObject;
            overlay.AddComponent<Image>().color=new Color(0,0,0,.75f);
            overlay.AddComponent<UnityEngine.UI.Button>();
            rect.localPosition=new Vector3(rect.localPosition.x,rect.localPosition.y,-20);
            rect.SetAsLastSibling();
            panel=NativeUi.Panel(rect,"FolderEditor",new Vector2(920,700));
            ShowHome();
        }
        static void ClearPanel()
        {
            foreach(Transform child in panel) {child.gameObject.SetActive(false);UnityEngine.Object.Destroy(child.gameObject);}
        }
        static void Title(string subtitle)
        {
            NativeUi.Label(panel,"文件夹："+folder.FileName,28,new Vector2(850,75),new Vector2(.5f,1),new Vector2(0,-48));
            message=NativeUi.Label(panel,subtitle,23,new Vector2(850,80),new Vector2(.5f,1),new Vector2(0,-130));
            NativeUi.Button(panel,"Close","关闭",new Vector2(190,50),new Vector2(.5f,0),new Vector2(0,43),Close);
        }
        static void ShowHome()
        {
            ClearPanel();
            Title("已选中该文件夹");
            NativeUi.Button(panel,"FolderFormat","统一设置文件夹内视频的 VR 格式",new Vector2(740,66),new Vector2(.5f,.5f),new Vector2(0,55),ShowFormats);
            NativeUi.Button(panel,"DeleteFolder","删除整个文件夹…",new Vector2(740,66),new Vector2(.5f,.5f),new Vector2(0,-55),ConfirmDelete);
            NativeUi.Label(panel,"格式设置只包含直接子视频，不包含子文件夹。",21,new Vector2(850,70),new Vector2(.5f,0),new Vector2(0,140));
        }
        static string FormatName(int index)
        {
            object[] values=ConstValue.TextureTypesMap[index];
            var angle=(TextureAngleType)values[1];
            var direction=(TextureDirection)values[2];
            var category=(TextureCategory)values[0];
            string projection=angle==TextureAngleType.NoVR?"平面":angle.ToString().Replace("Angle","")+"°";
            string stereo=direction==TextureDirection.Same?"单目":direction==TextureDirection.SideBySide?"左右":"上下";
            return (category==TextureCategory.Normal?"":category==TextureCategory.Eac?"EAC ":"鱼眼 ")+projection+" / "+stereo;
        }
        static void ShowFormats()
        {
            ClearPanel();
            Title("选择统一格式（仅直接子视频）");
            int i=0;
            foreach(int key in ConstValue.TextureTypesMap.Keys.OrderBy(x=>x))
            {
                int code=key;
                NativeUi.Button(panel,"Format"+key,FormatName(key),new Vector2(280,46),new Vector2(.5f,1),new Vector2((i%3-1)*295,-205-(i/3)*52),delegate {SetFormat(code);});
                i++;
            }
        }
        static void SetFormat(int format)
        {
            var database=SingletonMonoBehaviour<DataBaseManager>.Singleton;
            var videos=database.GetAllVideoMeta(true).OfType<VideoFileInfo>()
                .Where(x=>Policy.IsDirectChild(folder.PathOrUrl,x.PathOrUrl)).ToDictionary(x=>x.PathOrUrl,StringComparer.OrdinalIgnoreCase);
            foreach(var info in BulkSelection.CurrentList(viewType).OfType<VideoFileInfo>().Where(x=>Policy.IsDirectChild(folder.PathOrUrl,x.PathOrUrl))) videos[info.PathOrUrl]=info;
            if(Directory.Exists(folder.PathOrUrl))
            {
                foreach(var path in Directory.EnumerateFiles(folder.PathOrUrl))
                {
                    if(!HelperScript.IsVideo(path)||videos.ContainsKey(path)) continue;
                    var info=new LocalVideoFileInfo(new FileInfo(path));
                    database.InsertVideoMeta(info);
                    videos[path]=info;
                }
            }
            // Retain a direct-child rule so unopened network videos use the format after browsing/restart.
            FolderFormats.Set(Hooks.FolderPath,folder.PathOrUrl,format);
            foreach(var video in videos.Values)
            {
                video.UserSetType=format;
                database.UpdateMediaMeta(video,DBMediaTableColumn.UserSetType,format.ToString());
            }
            SingletonMonoBehaviour<FileScrollViewManager>.Singleton.SetAllViewDataDirty(viewType);
            ClearPanel();
            Title("已设置为 "+FormatName(format)+"\n已更新 "+videos.Count+" 个视频；直接子视频将继续使用此格式。");
            if(SettingsStore.Load(Hooks.DefaultsPath)!=null)
                NativeUi.Label(panel,"播放参数按单视频→文件夹→全局应用，VR 格式单独保留。",22,new Vector2(850,90),new Vector2(.5f,.5f),Vector2.zero);
            Debug.Log("[SkyboxUsability] folder format saved; known video count="+videos.Count);
        }
        static int CountFiles(string path)
        {
            int count=0;
            var stack=new Stack<string>();stack.Push(path);
            while(stack.Count>0)
            {
                string next=stack.Pop();
                if((File.GetAttributes(next)&FileAttributes.ReparsePoint)!=0) throw new IOException("文件夹内含链接，请先在 Windows 中处理链接。");
                foreach(string file in Directory.EnumerateFiles(next))
                {
                    if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0) throw new IOException("文件夹内含链接，请先在 Windows 中处理链接。");
                    count++;
                }
                foreach(string dir in Directory.EnumerateDirectories(next)) stack.Push(dir);
            }
            return count;
        }
        static void ConfirmDelete(){ConfirmDelete(false);}
        static void ConfirmDelete(bool includeFavorites)
        {
            ClearPanel();
            Title("正在检查文件夹内容…");
            if(!Policy.CanDeleteFolder(folder.PathOrUrl))
            {
                message.text="此文件夹不能直接删除。仅支持可写的本地/Windows 共享文件夹，磁盘根目录和链接不能删除。";
                return;
            }
            try
            {
                int count=CountFiles(folder.PathOrUrl);
                int favorites=DeletionProtection.InFolder(folder.PathOrUrl);
                confirmedFavorites=includeFavorites?favorites:0;
                message.text="确认删除“"+folder.FileName+"”？\n包含 "+count+" 个文件，其中收藏视频 "+favorites+" 个（包括子文件夹）。";
                if(favorites>0&&!includeFavorites)
                {
                    NativeUi.Label(panel,"该文件夹包含收藏视频，默认不执行整夹删除。\n继续后，整个文件夹及收藏视频都会被移至回收站。",23,new Vector2(850,110),new Vector2(.5f,.5f),new Vector2(0,25));
                    NativeUi.Button(panel,"ReviewFavoriteFolder","继续确认删除（包含收藏）…",new Vector2(620,60),new Vector2(.5f,.5f),new Vector2(0,-95),delegate{ConfirmDelete(true);});
                    return;
                }
                NativeUi.Label(panel,"删除会将整个文件夹移至 Windows 回收站。",22,new Vector2(850,80),new Vector2(.5f,.5f),new Vector2(0,30));
                NativeUi.Button(panel,"ConfirmDelete",favorites>0?"确认删除整个文件夹（含收藏）":"确认删除整个文件夹",new Vector2(620,60),new Vector2(.5f,.5f),new Vector2(0,-80),DeleteFolder);
            }
            catch(Exception e) {message.text="无法检查文件夹："+e.Message;}
        }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        struct ShellOperation
        {
            public IntPtr hwnd;
            public uint function;
            [MarshalAs(UnmanagedType.LPWStr)] public string from;
            [MarshalAs(UnmanagedType.LPWStr)] public string to;
            public ushort flags;
            [MarshalAs(UnmanagedType.Bool)] public bool aborted;
            public IntPtr mappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string title;
        }
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SHFileOperation(ref ShellOperation operation);
        static void DeleteFolder()
        {
            if(DeletionProtection.InFolder(folder.PathOrUrl)>confirmedFavorites){ConfirmDelete(false);return;}
            // Capture the selected directory before asynchronous database notifications can
            // rebind cells. Use public deletion APIs so native row-ID mappings are populated.
            var selected=folder;
            var type=viewType;
            var manager=SingletonMonoBehaviour<DataBaseManager>.Singleton;
            var videos=manager.GetAllVideoMeta(true).Where(x=>Policy.IsInFolder(selected.PathOrUrl,x.PathOrUrl)).Select(x=>x.PathOrUrl).ToArray();
            var pictures=manager.GetAllPictureMeta(true).Where(x=>Policy.IsInFolder(selected.PathOrUrl,x.PathOrUrl)).Select(x=>x.PathOrUrl).ToArray();
            var directories=manager.GetAllDirectoryUrl(selected.FileFolderType).Where(x=>Policy.IsInFolder(selected.PathOrUrl,x)).ToArray();
            deletion.Execute(delegate
            {
                if(!Policy.CanDeleteFolder(selected.PathOrUrl)) {message.text="文件夹状态已改变，请关闭后重新选择。";return false;}
                CountFiles(selected.PathOrUrl);
                string target=Path.GetFullPath(selected.PathOrUrl);
                var operation=new ShellOperation{function=3,from=target+'\0'+'\0',flags=0x40|0x10|0x400|0x4};
                int result=SHFileOperation(ref operation);
                if(result!=0||operation.aborted||Directory.Exists(target))
                {
                    message.text="删除未完成，错误码："+result+"。文件夹未从媒体库移除。";
                    return false;
                }
                Debug.Log("[SkyboxUsability] selected folder moved to recycle bin");
                return true;
            },Close,delegate
            {
                foreach(string path in videos) manager.DeleteVideo(path);
                foreach(string path in pictures) manager.DeletePicture(path);
                foreach(string path in directories) manager.DeleteDirectory(path);
                FolderFormats.Remove(Hooks.FolderPath,selected.PathOrUrl);
            },delegate
            {
                // Refresh only the active list. Other channels can have uninitialized
                // USB/network managers and must not be forced to browse here.
                SingletonMonoBehaviour<FileScrollViewManager>.Singleton.SetFileListFromDBAndUpdate(type);
            });
        }
        public static void Close()
        {
            if(overlay!=null) {overlay.SetActive(false);UnityEngine.Object.Destroy(overlay);}
            overlay=null;panel=null;
        }
    }
}
