using System;
using System.IO;
using SkyboxVR;
using UnityEngine;

namespace SkyboxUsability
{
    public static class Hooks
    {
        static bool ready;
        public static string DataDirectory {get{return Path.Combine(Application.persistentDataPath,"SkyboxUsability");}}
        public static string DefaultsPath {get{return Path.Combine(DataDirectory,"playback-defaults.json");}}
        public static string FolderPath {get{return Path.Combine(DataDirectory,"folder-formats.json");}}
        public static void Ensure()
        {
            if(ready) return;
            ready=true;
            try
            {
                Directory.CreateDirectory(DataDirectory);
                BrowseControls.Initialize();
                BulkSelection.Initialize();
                StickShortcut.Ready=true;
                var main=SingletonMonoBehaviour<MainProcedure>.Singleton;
                if(main.GetComponent<StickShortcut>()==null)main.gameObject.AddComponent<StickShortcut>();
                Debug.Log("[SkyboxUsability] loaded 1.3 for 1.1.8(2668); list performance optimizations enabled");
            }
            catch(Exception e) {Error(e);}
        }
        public static void Error(Exception e) {Debug.LogError("[SkyboxUsability] "+e);}
        public static void DeleteUi(DeleteFileScript owner,bool show)
        {
            try {BulkSelection.Setup(owner,show);DeletionProtection.Refresh(owner);} catch(Exception e) {Error(e);}
        }
        public static void BindCell(FileItemEvent cell)
        {
            try
            {
                if(cell.NameText!=null && cell.NameText.font!=null) NativeUi.Font=cell.NameText.font;
                if(cell.GetComponent<CellTools>()==null) cell.gameObject.AddComponent<CellTools>();
                var details=cell.GetComponent<CardDetails>();
                if(details==null) details=cell.gameObject.AddComponent<CardDetails>();
                details.Refresh();
                var lifetime=cell.GetComponent<ThumbnailLifetime>();
                if(lifetime==null)lifetime=cell.gameObject.AddComponent<ThumbnailLifetime>();
                lifetime.Track();
            }
            catch(Exception e) {Error(e);}
        }
        public static bool SkipFolderClick(FileItemEvent cell)
        {
            var tools=cell.GetComponent<CellTools>();
            return tools!=null&&tools.ConsumeClick();
        }
        public static void SettingsUi(AdvancedPageManager owner)
        {
            try {Playback.Setup(owner);} catch(Exception e) {Error(e);}
        }
        public static void BeforeVideo(VideoFileInfo info)
        {
            try {Playback.Before(info);} catch(Exception e) {Error(e);}
        }
        public static void AfterVideo(VideoTextureManager manager,VideoFileInfo info)
        {
            try {Playback.After(manager,info);} catch(Exception e) {Error(e);}
        }
        public static void EyeSlider(BaseSliderControlScript owner)
        {
            try {LibraryDisplay.ExpandEyeSlider(owner);} catch(Exception e) {Error(e);}
        }
        public static void VideoEnded(VideoPlayerCtrl owner,bool hasError)
        {
            try {PlaybackFlow.BeginEnd(owner,hasError);} catch(Exception e) {PlaybackFlow.AutoAdvance=false;Error(e);}
        }
        public static void EndHandling(){PlaybackFlow.AutoAdvance=false;}
        public static bool AutoNext(VideoFileManager owner)
        {
            try{return PlaybackFlow.Next(owner);}catch(Exception e){Error(e);return PlaybackFlow.AutoAdvance;}
        }
        public static void PreserveProgress(VideoPlayerCtrl owner)
        {
            try{PlaybackFlow.PreserveProgress(owner);}catch(Exception e){Error(e);}
        }
        public static bool DeleteGate(VideoFileManager owner,DeleteFileState value)
        {
            try{DeletionProtection.Prepare(owner,value);return false;}
            catch(Exception e){Error(e);return value==DeleteFileState.Ok;}
        }
        public static void BrowseUi(FileScrollView owner)
        {
            try
            {
                LibraryDisplay.Completed.Contains(null);
                if(owner.GetComponent<BrowseControls>()==null)owner.gameObject.AddComponent<BrowseControls>();
            }
            catch(Exception e){Error(e);}
        }
        public static void FilterList(FileScrollView owner)
        {
            try{BrowseControls.Apply(owner);BulkSelection.ListChanged();}catch(Exception e){Error(e);}
        }
        public static void FavoriteReady(FavoriteButtonScript owner){StickShortcut.FavoriteControl=owner;}
        public static void ReleaseCell(FileItemEvent cell)
        {
            var lifetime=cell.GetComponent<ThumbnailLifetime>();
            if(lifetime!=null)lifetime.Release();
        }
        public static System.Collections.Generic.List<string> ScratchKeys(FileScrollView owner){return ViewScratch.Keys(owner);}
        public static System.Collections.Generic.HashSet<string> ScratchVisible(FileScrollView owner){return ViewScratch.Visible(owner);}
    }
}
