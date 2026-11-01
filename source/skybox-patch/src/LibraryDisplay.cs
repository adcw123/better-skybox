using System;
using System.Collections.Generic;
using System.Reflection;
using SkyboxVR;
using UnityEngine;
using UnityEngine.UI;

namespace SkyboxUsability
{
    public static class LibraryDisplay
    {
        static CompletedStore completed;
        public static CompletedStore Completed {get {return completed??(completed=new CompletedStore(System.IO.Path.Combine(Hooks.DataDirectory,"completed-videos.json")));}}
        public static void ExpandEyeSlider(BaseSliderControlScript slider)
        {
            if(!(slider is Offset3DSliderScript)) return;
            var values=NativeUi.Field<List<float>>(slider,"_valueList");
            var expanded=LibraryPolicy.EyeValues(values);
            values.Clear();values.AddRange(expanded);
            Debug.Log("[SkyboxUsability] eye distance range -1..1; values="+values.Count);
        }
        public static void VideoEnded(VideoPlayerCtrl player,bool hasError)
        {
            if(!(player.CurrentMediaFileInfo is VideoFileInfo)) return;
            bool playing=player.CurrentState==VideoplayerState.PLAYING||player.CurrentState==VideoplayerState.PAUSED;
            var get=typeof(VideoPlayerCtrl).GetMethod("GetProperty",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(string)},null);
            long reason=(long)get.Invoke(player,new object[]{"eof-reached"});
            if(!LibraryPolicy.IsCompleted(reason,hasError,playing)) return;
            Completed.Mark(player.CurrentMediaFileInfo.PathOrUrl);
            Debug.Log("[SkyboxUsability] video marked completed");
        }
    }
    public sealed class CardDetails:MonoBehaviour
    {
        FileItemEvent cell;
        Text details;
        RectTransform nameRect;
        Vector2 originalSize,originalPosition;
        bool compressed;
        bool separateLine;
        static bool loggedLayout;
        int metadataWidth=int.MinValue,metadataHeight;
        long metadataSize;
        string metadataText;
        string Metadata(VideoFileInfo video)
        {
            if(metadataText==null||metadataWidth!=video.Width||metadataHeight!=video.Height||metadataSize!=video.FileSize)
            {
                metadataWidth=video.Width;metadataHeight=video.Height;metadataSize=video.FileSize;
                metadataText=LibraryPolicy.Details(video.Width,video.Height,video.FileSize);
            }
            return metadataText;
        }
        void Awake() {cell=GetComponent<FileItemEvent>();}
        public void Refresh()
        {
            var video=cell.FileInfo as VideoFileInfo;
            bool show=video!=null&&cell.NameText!=null;
            if(!show)
            {
                if(details!=null) details.gameObject.SetActive(false);
                RestoreName();return;
            }
            if(details==null)
            {
                nameRect=cell.NameText.rectTransform;
                originalSize=nameRect.sizeDelta;originalPosition=nameRect.anchoredPosition;
                separateLine=nameRect.rect.height>=48;
                if(!loggedLayout)
                {
                    loggedLayout=true;
                    Debug.Log("[SkyboxUsability] card name area="+nameRect.rect.size+"; pivot="+nameRect.pivot+"; separate metadata line="+separateLine);
                }
                // Compact list rows already have a dedicated size column.
                if(!separateLine&&cell.SizeText!=null)
                {
                    cell.SizeText.text=Metadata(video);
                    cell.SizeText.resizeTextForBestFit=true;
                    cell.SizeText.resizeTextMinSize=12;
                    cell.SizeText.resizeTextMaxSize=20;
                    UpdateProgress(video);return;
                }
                details=NativeUi.Label(nameRect,"",20,Vector2.zero,new Vector2(.5f,0),Vector2.zero);
                details.name="SkyboxVideoDetails";
                details.font=cell.NameText.font;
                details.alignment=TextAnchor.MiddleLeft;
                details.horizontalOverflow=HorizontalWrapMode.Overflow;
                details.resizeTextForBestFit=true;details.resizeTextMinSize=12;details.resizeTextMaxSize=20;
                var r=details.rectTransform;
                r.anchorMin=new Vector2(0,0);r.anchorMax=new Vector2(1,0);r.pivot=new Vector2(.5f,0);
                r.sizeDelta=new Vector2(0,24);r.anchoredPosition=new Vector2(0,-24);
                r.localPosition=new Vector3(r.localPosition.x,r.localPosition.y,-1);
            }
            if(!compressed)
            {
                // Reserve the bottom line inside the existing card; do not increase
                // grid height or put information over the next row's thumbnail.
                nameRect.sizeDelta=originalSize-new Vector2(0,24);
                nameRect.anchoredPosition=originalPosition+new Vector2(0,24*(1-nameRect.pivot.y));
                compressed=true;
            }
            details.gameObject.SetActive(true);
            details.text=Metadata(video);
            UpdateProgress(video);
        }
        void UpdateProgress(VideoFileInfo video)
        {
            if(cell.ProgressImage!=null&&LibraryDisplay.Completed.Contains(video.PathOrUrl))
            {
                var track=cell.ProgressImage.transform.parent as RectTransform;
                track.gameObject.SetActive(true);
                var bar=cell.ProgressImage.rectTransform;
                float width=track.rect.width;
                if(width<=0) width=track.sizeDelta.x;
                bar.sizeDelta=new Vector2(width,bar.sizeDelta.y);
            }
        }
        void RestoreName()
        {
            if(!compressed||nameRect==null) return;
            nameRect.sizeDelta=originalSize;nameRect.anchoredPosition=originalPosition;compressed=false;
        }
    }
}
