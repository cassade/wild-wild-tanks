using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tanks.Core
{
    public interface IGameObject
    {
        int    X               { get; set; } // координата левого нижнего угла в мировых координатах
        int    Y               { get; set; } // координата левого нижнего угла в мировых координатах
        int    Width           { get; }      // ширина объекта
        int    Height          { get; }      // высота объекта
        string ResourceId      { get; }      // определяет используемый спрайт и размер кадра
        int    AnimationLength { get; }      // количество кадров анимации
        int    Frame           { get; set; } // zero-based индекс текущего кадра
        bool   IsDisabled      { get; set; } // флаг [не]использования объекта в игре
    }
}
