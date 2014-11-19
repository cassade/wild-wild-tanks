using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Tanks.Core.Objects
{
    public sealed class Bullet : IBullet
    {
        [JsonConstructor]
        public Bullet(string resourceId, ITank tank)
        {
            ResourceId = resourceId;
            Tank = tank;
            Direction = tank.Direction;
        }

        public int       X               { get; set; }
        public int       Y               { get; set; }
        public int       Width           { get { return 16; } }
        public int       Height          { get { return 16; } }
        public string    ResourceId      { get; private set; }
        public int       AnimationLength { get { return 1; } }
        public int       Frame           { get; set; }
        public int       Speed           { get { return 12; } }
        public Direction Direction       { get; private set; }
        public ITank     Tank            { get; private set; }
        public bool      IsDisabled      { get; set; }
    }
}