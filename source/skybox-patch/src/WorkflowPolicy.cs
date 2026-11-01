using System;
using System.Collections.Generic;
using System.Linq;
namespace SkyboxUsability
{
    public enum WatchState {Unwatched,Partial,Completed}
    public enum WatchFilter {All,Unwatched,Partial,Completed}
    public static class WorkflowPolicy
    {
        public static WatchState State(bool completed,int progress,long played) {return completed?WatchState.Completed:progress>0||played>0?WatchState.Partial:WatchState.Unwatched;}
        public static bool Matches(WatchFilter filter,WatchState state) {return filter==WatchFilter.All||(int)filter==(int)state+1;}
        public static int Next(int current,int count,bool wrap,Func<int,bool> eligible)
        {
            if(count<=0) return -1;
            for(int offset=1;offset<=count;offset++)
            {
                int next=current+offset;
                if(!wrap&&next>=count) break;
                next=((next%count)+count)%count;
                if(next!=current&&eligible(next)) return next;
            }
            return -1;
        }
        public static int Resume(double position,long duration,bool completed)
        {
            if(completed||double.IsNaN(position)||double.IsInfinity(position)||position<0) return 0;
            double max=duration>0?Math.Max(0,duration-1):int.MaxValue;
            return (int)Math.Min(int.MaxValue,Math.Min(position,max));
        }
        public static int RemoveProtected<T>(IDictionary<string,T> selected,Func<string,T,bool> protect,Action<T> deselect)
        {
            var keys=selected.Where(x=>protect(x.Key,x.Value)).Select(x=>x.Key).ToArray();
            foreach(var key in keys){deselect(selected[key]);selected.Remove(key);}
            return keys.Length;
        }
    }
    public enum StickAction {None,Reset,Favorite}
    public sealed class StickPress
    {
        double began;
        bool down,fired;
        string path;
        public StickAction Update(bool pressed,double now,string key)
        {
            if(path!=key){Clear();path=key;if(!pressed)return StickAction.None;}
            if(pressed&&!down){down=true;fired=false;began=now;}
            if(pressed&&down&&!fired&&now-began>=.7){fired=true;return StickAction.Favorite;}
            if(!pressed&&down){down=false;return fired?StickAction.None:StickAction.Reset;}
            return StickAction.None;
        }
        public void Clear(){path=null;down=false;fired=false;}
    }
}
