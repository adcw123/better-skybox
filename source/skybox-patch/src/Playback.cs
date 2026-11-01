using System;
using System.Linq;
using SkyboxVR;
using UnityEngine;
using UnityEngine.UI;

namespace SkyboxUsability
{
    public static class Playback
    {
        static PlaybackDefaults applying;
        static Text status;
        static GameObject settingsRoot;
        static PlaybackDefaults baseline;
        static bool previousScoped;
        public static string ScopedPath {get{return System.IO.Path.Combine(Hooks.DataDirectory,"scoped-playback.json");}}
        public static PlaybackDefaults Capture()
        {
            var texture=SingletonMonoBehaviour<VideoTextureManager>.Singleton;
            var player=SingletonMonoBehaviour<VideoPlayerCtrl>.Singleton;
            var info=player.CurrentMediaFileInfo;
            int format=ConstValue.TextureTypesMap.First(x=>(TextureCategory)x.Value[0]==texture.CurTextureCategory&&(TextureAngleType)x.Value[1]==texture.CurTextureAngleType&&(TextureDirection)x.Value[2]==texture.CurTextureDirection).Key;
            return new PlaybackDefaults
            {
                Format=format, Ratio=(int)texture.CurVideoRatio, Height=info.ScreenHeight,
                Rotate=texture.RotationForScreen, Zoom=info.VRZoom, Offset=info.Offset3D,
                Tilt=VideoTextureManager.Tilt2DValue,Scale=PlayerPrefsSetting.GetScreenScale(),
                Contrast=PlayerPrefsSetting.GetContrast(),Luminance=PlayerPrefsSetting.GetLuminance(),Saturation=PlayerPrefsSetting.GetSaturation(),
                Mono=texture.IsForceMonoscopic,ExchangeEye=texture.IsExchangeEyeTexture,FullSbs=texture.PlaneVideoFullScreenSbs,
                Curved=texture.IsCurveScreen,FrameLock=texture.IsFrameLock,Speed=player.CurrentVideoPlaybackSpeedIndex
            };
        }
        static void Fill(MediaFileInfo info,PlaybackDefaults defaults,bool setFormat=true)
        {
            if(setFormat) info.UserSetType=defaults.Format;
            info.AspectRatio=defaults.Ratio;
            info.ScreenHeight=defaults.Height;
            info.ScreenRotate=defaults.Rotate;
            info.VRZoom=defaults.Zoom;
            info.Offset3D=defaults.Offset;
            info.Monoscopic=defaults.Mono;
            info.ExchangeEye=defaults.ExchangeEye;
            info.FullScreenSBS=defaults.FullSbs;
        }
        public static void Before(VideoFileInfo info)
        {
            applying=null;
            if(info==null) return;
            int? format=FolderFormats.Find(Hooks.FolderPath,info.PathOrUrl);
            if(format.HasValue) info.UserSetType=format.Value;
            var video=ScopedDefaults.FindVideo(ScopedPath,info.PathOrUrl);
            var folder=ScopedDefaults.FindFolder(ScopedPath,info.PathOrUrl);
            var global=SettingsStore.Load(Hooks.DefaultsPath);
            bool scoped=video!=null||folder!=null;
            if(scoped&&baseline==null) baseline=Capture();
            applying=ScopedDefaults.Resolve(video,folder,global);
            if(applying!=null) Fill(info,applying,!scoped&&!format.HasValue);
            else if(previousScoped) applying=baseline;
            previousScoped=scoped;
        }
        public static void After(VideoTextureManager texture,VideoFileInfo info)
        {
            if(applying==null||info==null) return;
            var defaults=applying;
            applying=null;
            Apply(texture,defaults);
        }
        static void Apply(VideoTextureManager texture,PlaybackDefaults defaults)
        {
            PlayerPrefsSetting.SetScreenScale(defaults.Scale);
            PlayerPrefsSetting.SetContrast(defaults.Contrast);
            PlayerPrefsSetting.SetLuminance(defaults.Luminance);
            PlayerPrefsSetting.SetSaturation(defaults.Saturation);
            texture.SetContrast(defaults.Contrast);
            texture.SetLuminance(defaults.Luminance);
            texture.SetSaturation(defaults.Saturation);
            texture.IsCurveScreen=defaults.Curved;
            texture.SetMeshScale(defaults.Scale);
            texture.IsFrameLock=defaults.FrameLock;
            VideoTextureManager.Tilt2DValue=defaults.Tilt;
            texture.SetScreenTilt(defaults.Tilt,false);
            SingletonMonoBehaviour<VideoPlayerCtrl>.Singleton.SetPlaybackSpeed(defaults.Speed);
            PlayerPrefs.Save();
        }
        public static void Save()
        {
            var player=SingletonMonoBehaviour<VideoPlayerCtrl>.Singleton;
            if(!(player.CurrentMediaFileInfo is VideoFileInfo)) {status.text="请先播放视频，再保存设置";return;}
            SettingsStore.Save(Hooks.DefaultsPath,Capture());
            status.text="已保存全局默认；单视频／文件夹保存的参数优先";
            Debug.Log("[SkyboxUsability] global playback defaults saved");
        }
        public static void SaveScoped(bool folder)
        {
            var player=SingletonMonoBehaviour<VideoPlayerCtrl>.Singleton;
            var info=player.CurrentMediaFileInfo as VideoFileInfo;
            if(info==null){status.text="请先播放视频";return;}
            if(folder)
            {
                string parent=ScopedDefaults.Parent(info.PathOrUrl);
                if(string.IsNullOrEmpty(parent)){status.text="此视频没有可用文件夹路径";return;}
                ScopedDefaults.SaveFolder(ScopedPath,parent,Capture());
                status.text="已保存当前文件夹的播放参数（仅直接子视频）";
            }
            else
            {
                ScopedDefaults.SaveVideo(ScopedPath,info.PathOrUrl,Capture());
                status.text="已保存当前视频的播放参数";
            }
            Debug.Log("[SkyboxUsability] scoped playback saved; folder="+folder);
        }
        public static void ResetScreenOnly()
        {
            var player=SingletonMonoBehaviour<VideoPlayerCtrl>.Singleton;
            var info=player.CurrentMediaFileInfo as VideoFileInfo;
            if(info==null)return;
            if(baseline==null)baseline=Capture();
            previousScoped=true;
            var texture=SingletonMonoBehaviour<VideoTextureManager>.Singleton;
            info.ScreenHeight=0;info.ScreenRotate=0;info.VRZoom=0;
            VideoTextureManager.Tilt2DValue=0;
            texture.SetTextureMeshHight(0);texture.SetTextureZoom(0);texture.SetScreenRotate(0);
            texture.SetScreenTilt(0,false);
            var db=SingletonMonoBehaviour<DataBaseManager>.Singleton;
            db.UpdateMediaMeta(info,DBMediaTableColumn.ScreenHeight,"0");
            db.UpdateMediaMeta(info,DBMediaTableColumn.ScreenRotate,"0");
            db.UpdateMediaMeta(info,DBMediaTableColumn.VRZoom,"0");
            // Persist only this video's resulting values; folder/global files are untouched.
            ScopedDefaults.SaveVideo(ScopedPath,info.PathOrUrl,Capture());
            if(settingsRoot!=null) foreach(var slider in settingsRoot.GetComponentsInChildren<BaseSliderControlScript>(true))
            {
                if(!(slider is TiltSliderScript||slider is HeightSliderScript||slider is ZoomSliderScript||slider is RotateSliderScript))continue;
                var values=NativeUi.Field<System.Collections.Generic.List<float>>(slider,"_valueList");
                int index=values.FindIndex(x=>Mathf.Approximately(x,0));
                if(index<0)continue;
                NativeUi.Field<Slider>(slider,"_slider").SetValueWithoutNotify(index);
                NativeUi.Field<Text>(slider,"_text").text="0";
            }
            if(status!=null)status.text="当前视频倾角／高度／距离／旋转已归零";
            Debug.Log("[SkyboxUsability] current video screen controls reset: tilt/height/distance/rotation=0");
        }
        public static void Reset()
        {
            SettingsStore.Clear(Hooks.DefaultsPath);
            var player=SingletonMonoBehaviour<VideoPlayerCtrl>.Singleton;
            var info=player.CurrentMediaFileInfo as VideoFileInfo;
            if(info!=null)
            {
                // Reset viewing parameters without changing projection/stereo encoding.
                // Read the active renderer rather than possibly stale per-video metadata.
                var defaults=PlaybackDefaults.ResetForFormat(Capture().Format);
                Fill(info,defaults);
                var texture=SingletonMonoBehaviour<VideoTextureManager>.Singleton;
                typeof(VideoTextureManager).GetMethod("InItVideoFileInfo",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(texture,new object[]{info});
                typeof(VideoTextureManager).GetMethod("ApplySetting",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(texture,null);
                Apply(texture,defaults);
            }
            status.text="已恢复播放参数，VR 格式保持不变；全局保存已清除";
            Debug.Log("[SkyboxUsability] global playback defaults reset; current VR format preserved");
        }
        public static void Setup(AdvancedPageManager owner)
        {
            var root=NativeUi.Field<GameObject>(owner,"_advancedSettingGo");
            if(root==null||root.transform.Find("SkyboxDefaultsToolbar")!=null) return;
            settingsRoot=root;
            var sample=root.GetComponentInChildren<Text>(true);
            if(sample!=null) NativeUi.Font=sample.font;
            var bar=NativeUi.Rect("SkyboxDefaultsToolbar",root.transform,new Vector2(720,154),new Vector2(.5f,1),new Vector2(0,82));
            bar.gameObject.AddComponent<Image>().color=new Color(.035f,.05f,.075f,.99f);
            NativeUi.Button(bar,"SaveDefaults","保存为全局默认",new Vector2(330,44),new Vector2(.25f,1),new Vector2(0,-27),Save);
            NativeUi.Button(bar,"ResetDefaults","恢复初始默认",new Vector2(330,44),new Vector2(.75f,1),new Vector2(0,-27),Reset);
            NativeUi.Button(bar,"SaveVideoDefaults","保存当前视频参数",new Vector2(330,44),new Vector2(.25f,1),new Vector2(0,-77),delegate{SaveScoped(false);});
            NativeUi.Button(bar,"SaveFolderDefaults","保存当前文件夹参数",new Vector2(330,44),new Vector2(.75f,1),new Vector2(0,-77),delegate{SaveScoped(true);});
            status=NativeUi.Label(bar,"参数优先级：当前视频 → 当前文件夹 → 全局默认",18,new Vector2(700,28),new Vector2(.5f,0),new Vector2(0,16));
        }
    }
}
