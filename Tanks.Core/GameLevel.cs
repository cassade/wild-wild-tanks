using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tanks.Core
{
    public sealed class GameLevel
    {
        public IList<IMiscellaneous> Miscellaneous { get; set; }
        public IList<ITank>          Enemies       { get; set; }
    }
}
