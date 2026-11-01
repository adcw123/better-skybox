using System;
using System.Collections;
using System.Reflection;
using SkyboxVR;
using UnityEngine;
namespace SkyboxUsability
{
    public static class PlaybackFlow
    {
        public static bool AutoAdvance;
        public static long EndReason(VideoPlayerCtrl player)
        {
            return (long)typeof(VideoPlayerCtrl).GetMethod("GetProperty",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(string)},null).Invoke(player,new object[]{"eof-reached"});
        }
        public static void BeginEnd(VideoPlayerCtrl player,bool hasError)
        {
            AutoAdvance=false;
            LibraryDisplay.VideoEnded(player,hasError);
            AutoAdvance=player.CurrentMediaFileInfo is VideoFileInfo&&!hasError&&EndReason(player)==1;
        }
        public static bool Next(VideoFileManager manager)
        {
            if(!AutoAdvance||!BrowseControls.SkipCompleted) return false;
            var player=SingletonMonoBehaviour<VideoPlayerCtrl>.Singleton;
            var list=manager.CurrentPlayList;
            int current=NativeUi.Field<int>(manager,"_currentPlayVideoFileIndex");
            int next=WorkflowPolicy.Next(current,list.Count,player.CurrentVideoLoopType!=VideoLoopType.NoLoop,i=>
            {
                var media=list[i] as MediaFileInfo;
                return media!=null&&(!(media is VideoFileInfo)||!LibraryDisplay.Completed.Contains(media.PathOrUrl));
            });
            if(next<0)
            {
                player.CurrentState=VideoplayerState.READY;
                SingletonMonoBehaviour<InterfaceAnimation>.Singleton.IsPlayUIShow=true;
                Debug.Log("[SkyboxUsability] autoplay stopped: no unwatched candidate");
                return true;
            }
            NativeUi.SetField(manager,"_currentPlayVideoFileIndex",next);
            ActionManager.OnSettingPanelShow?.Invoke(SettingPanelType.None);
            SingletonMonoBehaviour<InterfaceAnimation>.Singleton.IsPlayUIShow=false;
            var routine=(IEnumerator)typeof(VideoFileManager).GetMethod("PlayPreviousOrNextVideo",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,null);
            manager.StartCoroutine(routine);
            Debug.Log("[SkyboxUsability] autoplay selected unfinished video");
            return true;
        }
        public static void PreserveProgress(VideoPlayerCtrl player)
        {
            var info=player.CurrentMediaFileInfo as VideoFileInfo;
            if(info==null)return;
            bool ended=EndReason(player)==1;
            if(player.Width>0&&player.Height>0||ended)
                info.LastProgressTime=WorkflowPolicy.Resume(player.CurrentPosition,player.Duration>0?player.Duration:info.Duration,ended);
        }
    }
}
