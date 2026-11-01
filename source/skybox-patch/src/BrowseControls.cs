using System;
using System.Collections.Generic;
using SkyboxVR;
using UnityEngine;
using UnityEngine.UI;
namespace SkyboxUsability
{
    public class BrowseControls:MonoBehaviour
    {
        public static WatchFilter Filter;
        public static bool SkipCompleted=true;
        FileScrollView view;
        GameObject bar;
        Text filterLabel,skipLabel;
        bool supported,filterOnEnable=true;
        int displayedFilter=-1;
        bool displayedSkip;
        static readonly string[] names={"全部","未看过","看了一部分","已看完"};
        public static void Initialize()
        {
            Filter=(WatchFilter)Mathf.Clamp(PlayerPrefs.GetInt("SkyboxUsability.WatchFilter",0),0,3);
            SkipCompleted=PlayerPrefs.GetInt("SkyboxUsability.SkipCompleted",1)!=0;
        }
        static bool Supported(FileScrollView owner)
        {
            var type=NativeUi.Field<ScrollViewType>(owner,"_scrollViewType");
            switch(type)
            {
                case ScrollViewType.AllFiles:case ScrollViewType.Directory:case ScrollViewType.VrVideos:
                case ScrollViewType.NetworkDirectory:case ScrollViewType.AirScreen:case ScrollViewType.AirScreenDirectory:
                case ScrollViewType.HiddenFiles:case ScrollViewType.HiddenDirectory:case ScrollViewType.Favorite:case ScrollViewType.Recently:return true;
                default:return false;
            }
        }
        public static void Apply(FileScrollView owner)
        {
            if(Filter==WatchFilter.All||!Supported(owner))return;
            lock(NativeUi.Field<object>(owner,"_viewDataLock"))
            {
                var items=NativeUi.Field<List<BaseFileInfo>>(owner,"_needShowList");
                items.RemoveAll(item=>
                {
                    var video=item as VideoFileInfo;
                    return video!=null&&!WorkflowPolicy.Matches(Filter,WorkflowPolicy.State(LibraryDisplay.Completed.Contains(video.PathOrUrl),video.LastProgressTime,video.VideoPlayedTime));
                });
            }
        }
        void Awake(){view=GetComponent<FileScrollView>();supported=Supported(view);}
        void Update()
        {
            if(!supported)return;
            if(filterOnEnable){Apply(view);filterOnEnable=false;}
            if(bar==null)
            {
                var sample=view.transform.parent.GetComponentInChildren<Text>(true);
                if(sample!=null&&sample.font!=null)NativeUi.Font=sample.font;
                if(NativeUi.Font==null)return;
                // The original parent is AllFilesMask (RectMask2D). Moving its
                // child upward clips the entire toolbar. Host it on InterfaceDir,
                // above the panel edge, while retaining this view's visibility.
                Transform host=view.transform.parent;
                for(Transform ancestor=view.transform.parent;ancestor!=null;ancestor=ancestor.parent)
                {
                    if((ancestor.GetComponent<RectMask2D>()!=null||ancestor.GetComponent<Mask>()!=null)&&ancestor.parent!=null)host=ancestor.parent;
                    if(ancestor.name=="InterfaceDir"){host=ancestor;break;}
                }
                var r=NativeUi.Rect("SkyboxWatchToolbar",host,new Vector2(830,52),new Vector2(.5f,1),new Vector2(0,40));
                bar=r.gameObject;
                Debug.Log("[SkyboxUsability] toolbar host="+host.name+"; y="+r.anchoredPosition.y+"; clipping masks="+r.GetComponentsInParent<RectMask2D>(true).Length);
                bar.AddComponent<Image>().color=new Color(.035f,.05f,.075f,.96f);
                filterLabel=NativeUi.Button(r,"WatchFilter","",new Vector2(300,44),new Vector2(0,.5f),new Vector2(160,0),ChangeFilter);
                skipLabel=NativeUi.Button(r,"SkipCompleted","",new Vector2(470,44),new Vector2(1,.5f),new Vector2(-245,0),ToggleSkip);
            }
            bool visible=view.gameObject.activeInHierarchy&&!SingletonMonoBehaviour<VideoFileManager>.Singleton.IsDeleting;
            if(bar.activeSelf!=visible)bar.SetActive(visible);
            if(displayedFilter!=(int)Filter)
            {
                displayedFilter=(int)Filter;
                filterLabel.text="观看："+names[displayedFilter];
            }
            if(string.IsNullOrEmpty(skipLabel.text)||displayedSkip!=SkipCompleted)
            {
                displayedSkip=SkipCompleted;
                skipLabel.text="自动连播跳过已看完："+(SkipCompleted?"开":"关");
            }
        }
        void ChangeFilter()
        {
            Filter=(WatchFilter)(((int)Filter+1)%4);
            PlayerPrefs.SetInt("SkyboxUsability.WatchFilter",(int)Filter);PlayerPrefs.Save();
            var rect=NativeUi.Field<RectTransform>(view,"_contentRect");rect.anchoredPosition=Vector2.zero;
            view.SetFileListFromDBAndUpdate();
            Debug.Log("[SkyboxUsability] watch filter="+Filter);
        }
        void ToggleSkip()
        {
            SkipCompleted=!SkipCompleted;PlayerPrefs.SetInt("SkyboxUsability.SkipCompleted",SkipCompleted?1:0);PlayerPrefs.Save();
        }
        void OnDisable(){if(bar!=null)bar.SetActive(false);}
        void OnEnable(){filterOnEnable=true;}
        void OnDestroy(){if(bar!=null)Destroy(bar);}
    }
}
