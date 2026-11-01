using System;
using System.Reflection;
using HTC.UnityPlugin.Vive;
using SkyboxVR;
using UnityEngine;
namespace SkyboxUsability
{
    public sealed class StickShortcut:MonoBehaviour
    {
        readonly StickPress gesture=new StickPress();
        public static FavoriteButtonScript FavoriteControl;
        public static bool Ready;
        void Update()
        {
            try
            {
                var player=SingletonMonoBehaviour<VideoPlayerCtrl>.Singleton;
                var ui=SingletonMonoBehaviour<InterfaceAnimation>.Singleton;
                if(!Ready||player==null||ui==null||ui.IsInHomeInterface||!(player.CurrentMediaFileInfo is VideoFileInfo)||(player.CurrentState!=VideoplayerState.PLAYING&&player.CurrentState!=VideoplayerState.PAUSED)) {gesture.Clear();return;}
                bool press=ViveInput.GetPress(HandRole.RightHand,ControllerButton.Joystick);
                var action=gesture.Update(press,Time.unscaledTime,player.CurrentMediaFileInfo.PathOrUrl);
                if(action==StickAction.Reset)Playback.ResetScreenOnly();
                if(action==StickAction.Favorite)
                {
                    if(FavoriteControl==null){Debug.LogWarning("[SkyboxUsability] favorite control not ready");return;}
                    typeof(FavoriteButtonScript).GetMethod("OnButtonClicked",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(FavoriteControl,null);
                    bool saved=player.CurrentMediaFileInfo.VideoFavouritedTime!=0;
                    SingletonMonoBehaviour<TipsManager>.Singleton.ShowTips(saved?"已收藏":"已取消收藏",TipsManager.TipLevel.Succeed,null,1.5f);
                    Debug.Log("[SkyboxUsability] right stick hold toggled favorite="+saved+"; feedback shown");
                }
            }
            catch(Exception e){Hooks.Error(e);gesture.Clear();}
        }
    }
}
