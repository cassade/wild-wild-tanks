using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tanks.Core.Objects
{
    public sealed class Concrete : IMiscellaneous
    {
        private int health = 50;

        public int    X               { get; set; }
        public int    Y               { get; set; }
        public int    Width           { get { return 64; } }
        public int    Height          { get { return 64; } }
        public string ResourceId      { get { return "concrete"; } }
        public int    AnimationLength { get { return 1; } }
        public int    Frame           { get; set; }
        public bool   IsRoadway       { get { return false; } }
        public bool   IsBarrier       { get { return true; } }
        public int    Health          { get { return health; } set { health = value; } }
        public bool   IsDisabled      { get; set; }
    }
}
