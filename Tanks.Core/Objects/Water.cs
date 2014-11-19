using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tanks.Core.Objects
{
    public sealed class Water : IMiscellaneous
    {
        public int    X               { get; set; }
        public int    Y               { get; set; }
        public int    Width           { get { return 64; } }
        public int    Height          { get { return 64; } }
        public string ResourceId      { get { return "water"; } }
        public int    AnimationLength { get { return 1; } }
        public int    Frame           { get; set; }
        public bool   IsRoadway       { get { return false; } }
        public bool   IsBarrier       { get { return false; } }
        public int    Health          { get; set; }
        public bool   IsDisabled      { get; set; }
    }
}
