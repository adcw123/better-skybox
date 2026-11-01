using System;
using System.Collections.Generic;
using System.Linq;
using SkyboxVR;
using UnityEngine;
using UnityEngine.UI;

namespace SkyboxUsability
{
    public static class BulkSelection
    {
        public static int ListRevision,SelectionRevision;
        static readonly SelectionIndex rowIndices=new SelectionIndex();
        static bool listening;
        public static void Initialize()
        {
            if(listening)return;listening=true;
            ActionManager.OnCurrentDeleteCountChanged+=SelectionChanged;
            ActionManager.OnDeleteStateChanged+=delegate{SelectionChanged();};
            ActionManager.OnFileListViewTypeChanged+=delegate{ListChanged();};
        }
        static void SelectionChanged(){System.Threading.Interlocked.Increment(ref SelectionRevision);}
        public static void ListChanged(){System.Threading.Interlocked.Increment(ref ListRevision);}
        public static int RowIndex(List<BaseFileInfo> list,string path){return rowIndices.Find(list,ListRevision,()=>list.Select(x=>x.PathOrUrl),path);}
        public static List<BaseFileInfo> CurrentList(ScrollViewType type)
        {
            return SingletonMonoBehaviour<FileScrollViewManager>.Singleton.GetNeedShowList(type)??new List<BaseFileInfo>();
        }
        public static bool AllSelected(IEnumerable<MediaFileInfo> items)
        {
            bool any=false;var manager=SingletonMonoBehaviour<VideoFileManager>.Singleton;
            foreach(var item in items){any=true;if(!manager.IsSelected(item))return false;}
            return any;
        }
        public static void Toggle(IEnumerable<BaseFileInfo> items,ScrollViewType type)
        {
            var manager=SingletonMonoBehaviour<VideoFileManager>.Singleton;
            if(manager.CurrentDeleteFileState!=DeleteFileState.Selecting) return;
            var media=items.OfType<MediaFileInfo>().GroupBy(x=>x.PathOrUrl).Select(x=>x.First()).ToList();
            bool select=media.Any(x=>!manager.IsSelected(x));
            foreach(var info in media)
            {
                if(select&&!manager.IsSelected(info)) manager.AddToDeleteList(info);
                if(!select&&manager.IsSelected(info)) manager.RemoveFromDeleteList(info);
                info.IsDeleteSelected=select;
            }
            SingletonMonoBehaviour<FileScrollViewManager>.Singleton.SetAllViewDataDirty(type);
            ActionManager.OnInterfaceTitleNeedRefresh?.Invoke();
        }
        public static void Setup(DeleteFileScript owner,bool show)
        {
            var existing=owner.GetComponent<SelectAllControl>();
            if(existing==null) existing=owner.gameObject.AddComponent<SelectAllControl>();
            existing.Show(owner,show);
        }
        public static void SelectNonFavorites(ScrollViewType type)
        {
            var manager=SingletonMonoBehaviour<VideoFileManager>.Singleton;
            if(manager.CurrentDeleteFileState!=DeleteFileState.Selecting) return;
            foreach(var info in CurrentList(type).OfType<MediaFileInfo>())
            {
                bool select=LibraryPolicy.SelectNonFavorite(info is VideoFileInfo,info.VideoFavouritedTime);
                if(select&&!manager.IsSelected(info)) manager.AddToDeleteList(info);
                if(!select&&manager.IsSelected(info)) manager.RemoveFromDeleteList(info);
                info.IsDeleteSelected=select;
            }
            SingletonMonoBehaviour<FileScrollViewManager>.Singleton.SetAllViewDataDirty(type);
            ActionManager.OnInterfaceTitleNeedRefresh?.Invoke();
        }
    }
    public class SelectAllControl:MonoBehaviour
    {
        Text label;
        GameObject toolbar;
        GameObject nonFavorites;
        readonly RevisionStamp stamp=new RevisionStamp();
        public void Show(DeleteFileScript owner,bool show)
        {
            if(toolbar==null)
            {
                var delete=NativeUi.Field<GameObject>(owner,"_deleteButtonGo").GetComponent<RectTransform>();
                var cancel=NativeUi.Field<GameObject>(owner,"_cancelButtonGo").GetComponent<RectTransform>();
                var source=delete.GetComponentInChildren<Text>(true);
                if(source!=null) NativeUi.Font=source.font;
                // Keep the new control inside the header, immediately after Cancel.
                // Placing it before the leftmost original control put it outside the panel.
                Vector2 position=cancel.anchoredPosition+new Vector2((1-cancel.pivot.x)*cancel.rect.width+110,0);
                label=NativeUi.Button(cancel.parent,"SkyboxSelectAll","全选",new Vector2(180,54),cancel.anchorMin,position,delegate
                {
                    var type=SingletonMonoBehaviour<ScrollViewManager>.Singleton.CurScrollViewType;
                    BulkSelection.Toggle(BulkSelection.CurrentList(type),type);
                });
                toolbar=label.transform.parent.gameObject;
                var rect=toolbar.GetComponent<RectTransform>();
                rect.anchorMax=cancel.anchorMax;
                toolbar.AddComponent<LayoutElement>().ignoreLayout=true;
                var nonFavoriteLabel=NativeUi.Button(cancel.parent,"SkyboxSelectNonFavorites","全选非收藏",new Vector2(216,54),cancel.anchorMin,position+new Vector2(210,0),delegate
                {
                    BulkSelection.SelectNonFavorites(SingletonMonoBehaviour<ScrollViewManager>.Singleton.CurScrollViewType);
                });
                nonFavoriteLabel.fontSize=23;
                nonFavorites=nonFavoriteLabel.transform.parent.gameObject;
                nonFavorites.GetComponent<RectTransform>().anchorMax=cancel.anchorMax;
                nonFavorites.AddComponent<LayoutElement>().ignoreLayout=true;
            }
            toolbar.SetActive(show);
            if(nonFavorites!=null) nonFavorites.SetActive(show);
            stamp.Reset();
        }
        void Update()
        {
            if(toolbar==null||!toolbar.activeSelf) return;
            if(!stamp.Changed(BulkSelection.ListRevision,BulkSelection.SelectionRevision))return;
            var type=SingletonMonoBehaviour<ScrollViewManager>.Singleton.CurScrollViewType;
            label.text=BulkSelection.AllSelected(BulkSelection.CurrentList(type).OfType<MediaFileInfo>())?"取消全选":"全选";
        }
        void OnDestroy() {if(toolbar!=null) Destroy(toolbar);if(nonFavorites!=null) Destroy(nonFavorites);}
    }
}
