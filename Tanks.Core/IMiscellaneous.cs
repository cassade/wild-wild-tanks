using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tanks.Core
{
    public interface IMiscellaneous : IGameObject
    {
        bool IsRoadway { get; }
        bool IsBarrier { get; }
        int  Health    { get; set; }
    }
}
