using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tanks.Core
{
    public interface IBullet : IGameObject
    {
        ITank     Tank      { get; } // танк, выпустившиий снаряд, для контроля количества снарядов (один танк - один активный сняряд)
        Direction Direction { get; }
        int       Speed     { get; }
    }
}
