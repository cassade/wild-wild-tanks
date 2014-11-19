using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tanks.Core
{
    public interface ITank : IGameObject
    {
        int       Health        { get; set; }
        Direction Direction     { get; set; }
        int       Speed         { get; }
        int       FireDelay     { get; }      // задержка между выстрелами (количество кадров), определяющая скорострельность
        int       LastFireFrame { get; set; } // кадр, в котором был произведен последний выстрел

        IBullet Fire(); // фабричный метод для получения экземпляра текущего оружия танка
    }
}
