namespace RecorridoAR
{
    // The visible pointer does not depend on isUp; game actions still require an extended index.
    public static class IndexTracking
    {
        // Visual guidance remains available for either raised index, independently of
        // the hand locked for scoring. Never substitute a thumb or a stale position.
        public static Finger Guide(Packet packet,string preferred)
        {
            var candidate=preferred=="R1" || preferred=="L1"?Find(packet,preferred):null;
            if(candidate!=null && candidate.isUp)return candidate;
            var right=Find(packet,"R1");
            if(right!=null && right.isUp)return right;
            var left=Find(packet,"L1");
            return left!=null && left.isUp?left:null;
        }
        public static Finger Find(Packet packet,string id)
        {
            if(packet?.fingers==null)return null;
            foreach(var f in packet.fingers)
                if(f!=null && f.id==id && !float.IsNaN(f.x) && !float.IsNaN(f.y) &&
                   !float.IsInfinity(f.x) && !float.IsInfinity(f.y) && f.x>=0 && f.x<=1 && f.y>=0 && f.y<=1)return f;
            return null;
        }
        public static Finger Select(Packet packet,string locked,string preferred)
        {
            if(locked!=null)return Find(packet,locked);
            if(preferred!=null)return Find(packet,preferred);
            var right=Find(packet,"R1");var left=Find(packet,"L1");
            if(right!=null && right.isUp)return right;
            if(left!=null && left.isUp)return left;
            return right??left;
        }
    }
}
