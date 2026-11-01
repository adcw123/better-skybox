using System;
using System.Collections.Generic;
using System.Linq;
using SkyboxVR;
using UnityEngine;
using UnityEngine.UI;
namespace SkyboxUsability
{
    public static class DeletionProtection
    {
        public static Dictionary<string,BaseFileInfo> Selected(VideoFileManager manager){return NativeUi.Field<Dictionary<string,BaseFileInfo>>(manager,"_selectingFileDic");}
        public static int Count(VideoFileManager manager){return Selected(manager).Values.OfType<VideoFileInfo>().Count(x=>x.VideoFavouritedTime!=0);}
        public static void Prepare(VideoFileManager manager,DeleteFileState state)
        {
            if(state!=DeleteFileState.Ok)return;
            // Resolve current favourites again at the execution boundary, including
            // objects selected earlier in a different list.
            var known=SingletonMonoBehaviour<DataBaseManager>.Singleton.GetAllVideoMeta(true).OfType<VideoFileInfo>().Where(x=>x.VideoFavouritedTime!=0).Select(x=>x.PathOrUrl);
            var favorites=new HashSet<string>(known,StringComparer.Ordinal);
            var selected=Selected(manager);
            int removed=WorkflowPolicy.RemoveProtected(selected,(key,info)=>info is VideoFileInfo&&(((VideoFileInfo)info).VideoFavouritedTime!=0||favorites.Contains(key)),info=>((MediaFileInfo)info).IsDeleteSelected=false);
            if(removed>0){ActionManager.OnCurrentDeleteCountChanged?.Invoke();Debug.Log("[SkyboxUsability] protected favorites skipped="+removed);}
        }
        public static int InFolder(string folder)
        {
            return SingletonMonoBehaviour<DataBaseManager>.Singleton.GetAllVideoMeta(true).OfType<VideoFileInfo>().Count(x=>x.VideoFavouritedTime!=0&&Policy.IsInFolder(folder,x.PathOrUrl));
        }
        public static void Refresh(DeleteFileScript owner)
        {
            var panel=NativeUi.Field<GameObject>(owner,"_deletePanelGo");
            if(panel==null||!panel.activeSelf)return;
            var existing=panel.transform.Find("SkyboxFavoriteProtection");
            var text=existing==null?NativeUi.Label(panel.transform,"",22,new Vector2(620,70),new Vector2(.5f,0),new Vector2(0,110)):existing.GetComponent<Text>();
            text.name="SkyboxFavoriteProtection";
            var manager=SingletonMonoBehaviour<VideoFileManager>.Singleton;
            int favorites=Count(manager);
            text.text="包含 "+favorites+" 个收藏视频（默认跳过）\n本次将删除 "+(manager.CurDeleteCount-favorites)+" 个非收藏条目";
        }
    }
}
