using System;
using System.Reflection;
using System.Collections.Concurrent;
using SkyboxVR;
using UnityEngine;
using UnityEngine.UI;

namespace SkyboxUsability
{
    public static class NativeUi
    {
        public static Font Font;
        static readonly ConcurrentDictionary<Type,ConcurrentDictionary<string,FieldInfo>> fields=new ConcurrentDictionary<Type,ConcurrentDictionary<string,FieldInfo>>();
        static FieldInfo CachedField(object obj,string name)
        {
            Type type=obj.GetType();
            ConcurrentDictionary<string,FieldInfo> cache;
            if(!fields.TryGetValue(type,out cache))cache=fields.GetOrAdd(type,new ConcurrentDictionary<string,FieldInfo>());
            FieldInfo found;
            if(cache.TryGetValue(name,out found))return found;
            for(Type t=type;t!=null;t=t.BaseType)
            {
                var f=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
                if(f!=null){cache.TryAdd(name,f);return f;}
            }
            throw new MissingFieldException(type.Name,name);
        }
        public static T Field<T>(object obj,string name){return (T)CachedField(obj,name).GetValue(obj);}
        public static void SetField(object obj,string name,object value)
        {
            CachedField(obj,name).SetValue(obj,value);
        }
        public static RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 anchor,Vector2 position)
        {
            var go=new GameObject(name,typeof(RectTransform));
            go.layer=parent.gameObject.layer;
            var r=(RectTransform)go.transform;
            r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=anchor;
            r.sizeDelta=size;
            r.anchoredPosition=position;
            r.localRotation=Quaternion.identity;
            r.localScale=Vector3.one;
            r.localPosition=new Vector3(r.localPosition.x,r.localPosition.y,-1);
            return r;
        }
        public static Text Label(Transform parent,string text,int fontSize,Vector2 size,Vector2 anchor,Vector2 pos)
        {
            var rect=Rect("Label",parent,size,anchor,pos);
            var label=rect.gameObject.AddComponent<Text>();
            if(Font==null)
            {
                var existing=parent.GetComponentInChildren<Text>(true);
                if(existing!=null&&existing!=label) Font=existing.font;
            }
            label.font=Font;
            label.fontSize=fontSize;
            label.color=Color.white;
            label.alignment=TextAnchor.MiddleCenter;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;
            label.verticalOverflow=VerticalWrapMode.Truncate;
            label.raycastTarget=false;
            label.text=text;
            return label;
        }
        public static Text Button(Transform parent,string name,string text,Vector2 size,Vector2 anchor,Vector2 pos,Action action)
        {
            var r=Rect(name,parent,size,anchor,pos);
            var image=r.gameObject.AddComponent<Image>();
            Color normal=new Color(.13f,.20f,.29f,.98f);
            image.color=normal;
            var label=Label(r,text,24,size- new Vector2(12,4),new Vector2(.5f,.5f),Vector2.zero);
            var sprite=r.gameObject.AddComponent<SpriteButton>();
            SetField(sprite,"_deselectedNormalColor",normal);
            SetField(sprite,"_deselectedHoverColor",new Color(.22f,.44f,.66f,1));
            SetField(sprite,"_deselectedPressedColor",new Color(.12f,.60f,.76f,1));
            SetField(sprite,"_scaleValue",1f);
            // AddComponent has already run OnEnable with SpriteButton's default white
            // background. Apply the configured normal state immediately so white labels
            // are readable before the first pointer enter, also on inactive parents.
            sprite.SetIsSelected(false,false);
            sprite.OnButtonClicked+=delegate {try {action();} catch(Exception e) {Hooks.Error(e);} };
            return label;
        }
        public static RectTransform Panel(Transform parent,string name,Vector2 size)
        {
            var r=Rect(name,parent,size,new Vector2(.5f,.5f),Vector2.zero);
            var image=r.gameObject.AddComponent<Image>();
            image.color=new Color(.035f,.05f,.075f,.99f);
            // Consume pointer events over the panel so releases cannot activate a file behind it.
            r.gameObject.AddComponent<UnityEngine.UI.Button>();
            return r;
        }
    }
}
