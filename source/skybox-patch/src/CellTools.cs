using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using SkyboxVR;
using UnityEngine;
using UnityEngine.UI;

namespace SkyboxUsability
{
    public class CellTools:MonoBehaviour
    {
        FileItemEvent cell;
        SpriteButton button;
        Text rowLabel;
        Coroutine hold;
        bool suppressClick;
        string heldPath;
        float refresh;
        FileScrollView view;
        object viewGate;
        readonly RevisionStamp stamp=new RevisionStamp();
        string boundPath;
        static readonly PropertyInfo columnsProperty=typeof(FileScrollView).GetProperty("ItemPerRow",BindingFlags.Instance|BindingFlags.NonPublic);
        void Awake()
        {
            cell=GetComponent<FileItemEvent>();
            button=GetComponent<SpriteButton>();
            view=GetComponentInParent<FileScrollView>();
            viewGate=view==null?new object():NativeUi.Field<object>(view,"_viewDataLock");
            button.OnButtonDown+=Down;
            button.OnButtonUp+=Up;
            button.OnButtonExited+=Cancel;
        }
        void Down()
        {
            suppressClick=false;
            if(!(cell.FileInfo is DirectoryFileInfo) || SingletonMonoBehaviour<VideoFileManager>.Singleton.IsDeleting || button.IsDisabled) return;
            heldPath=cell.FileInfo.PathOrUrl;
            hold=StartCoroutine(Hold());
        }
        IEnumerator Hold()
        {
            yield return new WaitForSecondsRealtime(.7f);
            hold=null;
            if(cell.FileInfo==null||cell.FileInfo.PathOrUrl!=heldPath||button.IsDisabled) yield break;
            suppressClick=true;
            try { FolderEditor.Open(cell); } catch(Exception e) {Hooks.Error(e);}
        }
        void Up() {Cancel();}
        void Cancel() {if(hold!=null) {StopCoroutine(hold);hold=null;}}
        public bool ConsumeClick()
        {
            bool consumed=suppressClick;
            suppressClick=false;
            return consumed;
        }
        void Update()
        {
            if(Time.unscaledTime<refresh) return;
            refresh=Time.unscaledTime+.15f;
            try {Refresh();} catch(Exception e) {Hooks.Error(e);enabled=false;}
        }
        void Refresh()
        {
            var manager=SingletonMonoBehaviour<VideoFileManager>.Singleton;
            bool show=manager!=null&&manager.CurrentDeleteFileState==DeleteFileState.Selecting;
            if(!show){if(rowLabel!=null&&rowLabel.transform.parent.gameObject.activeSelf)rowLabel.transform.parent.gameObject.SetActive(false);stamp.Reset();return;}
            string path=cell.FileInfo==null?null:cell.FileInfo.PathOrUrl;
            if(path!=boundPath){boundPath=path;stamp.Reset();}
            if(!stamp.Changed(BulkSelection.ListRevision,BulkSelection.SelectionRevision))return;
            lock(viewGate)
            {
            var list=show?BulkSelection.CurrentList(cell.CurScrollViewType):null;
            int index=list==null||cell.FileInfo==null?-1:BulkSelection.RowIndex(list,path);
            int columns=view==null?1:(int)columnsProperty.GetValue(view,null);
            show=show&&index>=0&&index%columns==0;
            if(!show) {if(rowLabel!=null) rowLabel.transform.parent.gameObject.SetActive(false);return;}
            if(rowLabel==null)
            {
                rowLabel=NativeUi.Button(transform,"SkyboxSelectRow","本排全选",new Vector2(114,34),new Vector2(0,1),new Vector2(60,-20),SelectRow);
                rowLabel.fontSize=19;
            }
            if(!rowLabel.transform.parent.gameObject.activeSelf)rowLabel.transform.parent.gameObject.SetActive(true);
            var row=Policy.RowIndices(index,columns,list.Count).Select(x=>list[x]).OfType<MediaFileInfo>();
            rowLabel.text=BulkSelection.AllSelected(row)?"取消本排":"本排全选";
            }
        }
        void SelectRow()
        {
            lock(viewGate)
            {
            var list=BulkSelection.CurrentList(cell.CurScrollViewType);
            int index=list.FindIndex(x=>x.PathOrUrl==cell.FileInfo.PathOrUrl);
            int columns=view==null?1:(int)columnsProperty.GetValue(view,null);
            BulkSelection.Toggle(Policy.RowIndices(index,columns,list.Count).Select(x=>list[x]),cell.CurScrollViewType);
            }
        }
        void OnDisable() {Cancel();suppressClick=false;}
        void OnDestroy()
        {
            if(button!=null) {button.OnButtonDown-=Down;button.OnButtonUp-=Up;button.OnButtonExited-=Cancel;}
        }
    }
}
