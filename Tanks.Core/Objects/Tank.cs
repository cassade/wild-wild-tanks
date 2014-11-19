using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Tanks.Core.Objects
{
    public sealed class Tank : ITank
    {
        [JsonConstructor]
        public Tank(string resourceId, string bulletResourceId)
        {
            Health = 5;
            ResourceId = resourceId;
            BulletResourceId = bulletResourceId;
            IsDisabled = true;
        }

        public int            X                { get; set; }
        public int            Y                { get; set; }
        public int            Width            { get { return 64; } }
        public int            Height           { get { return 64; } }
        public string         ResourceId       { get; private set; }
        public string         BulletResourceId { get; private set; }
        public int            AnimationLength  { get { return 2; } }
        public int            Frame            { get; set; }
        public int            Health           { get; set; }
        public Direction      Direction        { get; set; }
        public int            Speed            { get { return 6; } }
        public float          Acceleration     { get; set; }
        public int            FireDelay        { get { return 0; } }
        public int            LastFireFrame    { get; set; }
        public bool           IsDisabled       { get; set; }

        public IBullet Fire()
        {
            return new Bullet(BulletResourceId, this);
        }
    }
}