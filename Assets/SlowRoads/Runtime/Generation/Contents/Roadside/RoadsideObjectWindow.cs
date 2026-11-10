using System;
using System.Collections.Generic;
namespace SlowRoads.Generation.Contents.Roadside
{
    /// <summary>E7 sorted ownership, S L46551-46603; update is a logical observer tick,
    /// not a render frame or a budget slice. Equal retirement keys preserve creation order.</summary>
    public sealed class RoadsideObjectWindow
    {
        readonly Dictionary<string,RoadsideObject> active=new();readonly List<RoadsideObject> order=new();
        public IEnumerable<RoadsideObject> Active=>order;public int Count=>order.Count;
        public event Action<RoadsideObject> Committed;public event Action<string> Released;
        public bool Contains(string id)=>active.ContainsKey(id);
        public void Put(RoadsideObject value)
        {
            if(active.Remove(value.Id,out var old))
            {
                int at=order.IndexOf(old);
                // A native h4.setVariant mutates the existing entry, not its E7
                // retirement order. Equal-key chevrons must retain their tie order.
                if(old.RetireIndex==value.RetireIndex){order[at]=value;active.Add(value.Id,value);Committed?.Invoke(value);return;}
                order.RemoveAt(at);
            }
            active.Add(value.Id,value);
            int i=0;while(i<order.Count&&order[i].RetireIndex<=value.RetireIndex)i++;order.Insert(i,value);Committed?.Invoke(value);
        }
        public void Remove(string id){if(active.Remove(id,out var value)){order.Remove(value);Released?.Invoke(id);}}
        public void Update(int observer)
        {if(order.Count>0&&observer>=order[0].RetireIndex)Remove(order[0].Id);if(order.Count>30)Remove(order[0].Id);}
        public void Clear(){foreach(var value in order)Released?.Invoke(value.Id);active.Clear();order.Clear();}
    }
}
