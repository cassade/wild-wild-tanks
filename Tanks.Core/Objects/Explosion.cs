using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tanks.Core.Objects
{
    public sealed class Explosion : IExplosion
    {
        public int    X               { get; set; }
        public int    Y               { get; set; }
        public int    Width           { get { return 64; } }
        public int    Height          { get { return 64; } }
        public string ResourceId      { get { return "explosion"; } }
        public int    AnimationLength { get { return 8; } }
        public int    Frame           { get; set; }
        public bool   IsDisabled      { get; set; }
    }
}
