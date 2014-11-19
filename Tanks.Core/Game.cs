using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Tanks.Core.Objects;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.System.Threading;

namespace Tanks.Core
{
    /// <summary>
    /// Содержит логику игры.
    /// </summary>
    /// <remarks>
    /// В связи с ограничениями, накладываемыми на WinRT компоненты (см. http://msdn.microsoft.com/en-us/library/br230301.aspx),
    /// в частности, запретом на использование наследования реализации,
    /// в классах предметной области определяются только основные, неизменяемые характеристики,
    /// основная логика игры реализована в сервисном классе <see cref="Game"/>.
    /// </remarks>
    public sealed class Game
    {
        #region Константы

        private const int fieldSizeModule = 64; // размер одного игрового модуля (квадратной области) в пикселях
        private const int fieldSize = 768;      // размер игрового поля в модулях
        private const int gameplayDelay = 120;  // пауза (количество кадров) между обновлениями состояния игры
        private const int enemiesConcurrentCount = 3;
        private const string suspendedFile = "suspended.json";
        private const string levelFile = "level{0}.json";

        #endregion

        #region Поля

        [JsonProperty] private IList<ITank> players = new List<ITank>(); // активный игрок один, но с ним удобнее работать в виде списка
        [JsonProperty] private IList<ITank> enemies = new List<ITank>();
        [JsonProperty] private IList<IMiscellaneous> miscellaneous = new List<IMiscellaneous>();
        [JsonProperty] private IList<IBullet> bullets = new List<IBullet>();
        [JsonProperty] private IList<IExplosion> explosions = new List<IExplosion>();
        [JsonProperty] private int currentFrame = 0;
        [JsonProperty] private int currentLevelNumber = 0;

        private int lastGamplayDelayFrame = 0;
        private Random rn = new Random();
        private Dictionary<VirtualKey, bool> isPressed = new Dictionary<VirtualKey, bool>()
        {
            { VirtualKey.W, false },
            { VirtualKey.A, false },
            { VirtualKey.S, false }, 
            { VirtualKey.D, false },
            { VirtualKey.Up, false }, 
            { VirtualKey.Down, false },
            { VirtualKey.Left, false },
            { VirtualKey.Right, false },
            { VirtualKey.Space, false },
        };
        private static JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
        {
            Binder = new TypeNameSerializationBinder(),
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            Formatting = Newtonsoft.Json.Formatting.Indented,
            TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple,
            TypeNameHandling = TypeNameHandling.Auto,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        };

        private ThreadPoolTimer timer;

        #endregion

        #region Конструктор

        /// <summary>
        /// Для получения экземпляра класса <see cref="Game"/> нужно использовать метод <see cref="Init"/>.
        /// </summary>
        public Game()
        {
        }

        #endregion
         
        #region События

        public event EventHandler<object> SoundRequested;

        #endregion

        #region Закрытые методы

        private static async Task<Game> Init()
        {
            // для облегчения публичного API для индикации возможности возобновления 
            // ранее сохраненной игры используем свойство CanResume,
            // инициализируем его значение синхронно;

            // на данный момент проверить существование файла можно только с помощью FileNotFoundException;

            var game = new Game();

            try
            {
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync(suspendedFile);
                game.CanResume = true;
            }
            catch (FileNotFoundException)
            {
                game.CanResume = false;
            }

            // создавать файлы в Package.Current.InstalledLocation нельзя,
            // поэтому для врзможности конструировать уровни копируем их в ApplicationData.Current.LocalFolder

            var localFiles = await ApplicationData.Current.LocalFolder.GetFilesAsync();
            if (localFiles.Count == 0)
            {
                var levelDirectory = await Package.Current.InstalledLocation.GetFolderAsync("resources\\levels");
                foreach (var file in await levelDirectory.GetFilesAsync())
                {
                    await file.CopyAsync(ApplicationData.Current.LocalFolder);
                }
            }

            return game;
        }

        private void Clear(bool clearPlayers = true)
        {
            // коллекции объектов очищаем

            if (clearPlayers)
            {
                players.Clear();
            }

            enemies.Clear();
            miscellaneous.Clear();
            bullets.Clear();
            explosions.Clear();

            // счетчики кадров и геймплея обнуляем

            currentFrame = 0;
            lastGamplayDelayFrame = 0;

            // сбрасываем флаги нажатий клавиш

            foreach (var key in isPressed.Keys.ToList())
            {
                isPressed[key] = false;
            }
        }

        private IEnumerable<T> Active<T>(IEnumerable<T> source) where T : IGameObject
        {
            return source.Where(obj => !obj.IsDisabled);
        }

        private bool HasCollision(IGameObject a, IGameObject b)
        {
            return HasCollision(a.X, a.Y, a.Width, a.Height, b.X, b.Y, b.Width, b.Height);
        }

        private bool HasCollision(int x1, int y1, int width1, int height1, int x2, int y2, int width2, int height2)
        {
            // поправка в 1px на каждый объект нужна для упрощения позиционирования танка,
            // иначе сложно попасть в проход шириной в один модуль

            return !(x1 + 1 > (x2 + width2 - 1) || (x1 + width1 - 1) < x2 + 1 || y1 + 1 > (y2 + height2 - 1) || (y1 + height1 - 1) < y2 + 1);
        }

        private Direction? GetDirectionFromPressedKey()
        {
            return 
                isPressed[VirtualKey.Up] ? Direction.Up :
                isPressed[VirtualKey.Down] ? Direction.Down :
                isPressed[VirtualKey.Left] ? Direction.Left :
                isPressed[VirtualKey.Right] ? Direction.Right :
                default(Direction?);
        }

        private void MoveTanks()
        {
            // игроки управляются пользователями с помощью
            // клавиатуры или экранного джойстика

            foreach (var tank in Active(players))
            {
                Move(tank, GetDirectionFromPressedKey());
            }

            // противники при встрече с препятствием
            // случайным образом меняют направление движения

            foreach (var tank in Active(enemies))
            {
                if (Move(tank, tank.Direction) == false)
                {
                    Move(tank, (Direction)(rn.Next(0, 4) * 90));
                }
            }
        }

        private bool Move(ITank tank, Direction? direction)
        {
            if (direction == null)
            {
                return false;
            }

            tank.Direction = direction.Value;

            tank.X += direction == Direction.Left ? (-1) * tank.Speed : direction == Direction.Right ? tank.Speed : 0;
            tank.Y += direction == Direction.Down ? (-1) * tank.Speed : direction == Direction.Up ? tank.Speed : 0;

            // гусеницы анимируем, сменяя поочередно кадры

            tank.Frame = tank.Frame == 0 ? 1 : 0;

            // за пределы экрана выезжать запрещено

            var stand = false;

            if (tank.X < 0)
            {
                tank.X = 0;
                stand = true;
            }

            if (tank.X + tank.Width > fieldSize)
            {
                tank.X = fieldSize - tank.Width;
                stand = true;
            }

            if (tank.Y < 0)
            {
                tank.Y = 0;
                stand = true;
            }

            if (tank.Y + tank.Height > fieldSize)
            {
                tank.Y = fieldSize - tank.Height;
                stand = true;
            }

            // при пересечении с другими объектами откатываем танк назад по 1px до тех пор,
            // пока сохраняется пересечение

            foreach (var t in Active(players).Concat(Active(enemies)))
            {
                if (t != tank)
                {
                    stand |= CorrectPosition(tank, t);
                }
            }

            foreach (var m in miscellaneous)
            {
                if (!m.IsRoadway)
                {
                    stand |= CorrectPosition(tank, m);
                }
            }

            return !stand;
        }

        private bool CorrectPosition(ITank tank, IGameObject obj)
        {
            var corrected = false;

            while (HasCollision(tank, obj))
            {
                tank.X += tank.Direction == Direction.Left ? 1 : tank.Direction == Direction.Right ? -1 : 0;
                tank.Y += tank.Direction == Direction.Down ? 1 : tank.Direction == Direction.Up ? -1 : 0;
                corrected = true;
            }

            return corrected;
        }

        private bool AreEnemies(ITank tank1, ITank tank2)
        {
            return
                (tank1.ResourceId.Contains("player") && !tank2.ResourceId.Contains("player")) ||
                (tank2.ResourceId.Contains("player") && !tank1.ResourceId.Contains("player"));
        }

        private void MoveBulletsAndCheckHits()
        {
            // обрабатываем попадания снярядов в различные объекты

            CheckBulletsHitWithBullets();
            CheckBulletsHitWithMiscellaneous();
            CheckBulletsHisWithTanks(players);
            CheckBulletsHisWithTanks(enemies);
            CheckBulletsOut();

            // перемещаем снаряды после обработки столкновений, т.е. делим обработку на два кадра,
            // давая снарядам долететь непосредственно до цели

            MoveBullets();
        }

        private void MoveBullets()
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                bullets[i].X += bullets[i].Direction == Direction.Left ? (-1) * bullets[i].Speed : bullets[i].Direction == Direction.Right ? bullets[i].Speed : 0;
                bullets[i].Y += bullets[i].Direction == Direction.Down ? (-1) * bullets[i].Speed : bullets[i].Direction == Direction.Up ? bullets[i].Speed : 0;
            }
        }

        private void CheckBulletsOut()
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                if (bullets[i].X < 0 || bullets[i].X + bullets[i].Width > fieldSize || bullets[i].Y < 0 || bullets[i].Y + bullets[i].Height > fieldSize)
                {
                    bullets.RemoveAt(i);
                }
            }
        }

        private void CheckBulletsHitWithMiscellaneous()
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                for (int j = miscellaneous.Count - 1; j >= 0; j--)
                {
                    if (miscellaneous[j].IsBarrier && HasCollision(bullets[i], miscellaneous[j]))
                    {
                        // уменьшаем здоровье объекта

                        miscellaneous[j].Health--;

                        // если здоровья больше нет - удаляем объект со сцены и анимируем его взрыв,
                        // иначе - анимируем взрыв снаряда

                        if (miscellaneous[j].Health == 0)
                        {
                            ExplodeBy<Explosion>(miscellaneous[j]);
                            miscellaneous.RemoveAt(j);
                        }
                        else
                        {
                            ExplodeBy<BulletExplosion>(bullets[i]);
                        }

                        // сняряд удаляется со сцены в любом случае

                        bullets.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private void CheckBulletsHitWithBullets()
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                for (int j = bullets.Count - 1; j >= 0; j--)
                {
                    if (bullets[i] != bullets[j] && HasCollision(bullets[i], bullets[j]) && AreEnemies(bullets[i].Tank, bullets[j].Tank))
                    {
                        // взрываем и удаляем со сцены оба снаряда,
                        // при этом счетчик внешнего цикла нужно дополнительно декрементировать,
                        // так как циклы по одному и тому же списку

                        ExplodeBy<BulletExplosion>(bullets[i]);
                        ExplodeBy<BulletExplosion>(bullets[j]);
                        bullets.RemoveAt(i);
                        bullets.RemoveAt(j);
                        i--;
                        break;
                    }
                }
            }
        }

        private void CheckBulletsHisWithTanks<T>(IList<T> tanks) where T : class, ITank
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                for (int j = tanks.Count - 1; j >= 0; j--)
                {
                    if (tanks[j] != bullets[i].Tank && !tanks[j].IsDisabled && HasCollision(bullets[i], tanks[j]) && AreEnemies(tanks[j], bullets[i].Tank))
                    {
                        // уменьшаем здоровье танка

                        tanks[j].Health--;

                        // если здоровья больше нет - удаляем танк со сцены, анимируем его взрыв,
                        // а также фиксируем момент уничтожения танка,
                        // иначе - анимируем взрыв снаряда

                        if (tanks[j].Health == 0)
                        {
                            ExplodeBy<Explosion>(tanks[j]);
                            tanks.RemoveAt(j);
                            lastGamplayDelayFrame = currentFrame;
                        }
                        else
                        {
                            ExplodeBy<BulletExplosion>(bullets[i]);
                        }

                        // сняряд удаляется со сцены в любом случае

                        bullets.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private void ExplodeBy<T>(IGameObject obj) where T : IExplosion, new()
        {
            var exp = new T();

            exp.X = obj.X + obj.Width / 2 - exp.Width / 2;
            exp.Y = obj.Y + obj.Height / 2 - exp.Height / 2;
            explosions.Add(exp);

            var soundEnumType = typeof(Sound);
            var explosionType = typeof(T);

            Play((Sound)Enum.Parse(soundEnumType, explosionType.Name));
        }

        private void AnimateExplosions()
        {
            for (int i = explosions.Count - 1; i >= 0; i--)
            {
                if (++explosions[i].Frame >= explosions[i].AnimationLength)
                {
                    explosions.RemoveAt(i);
                }
            }
        }

        private void Play(Sound sound)
        {
            var handler = SoundRequested;
            if (handler != null)
            {
                handler(this, sound.ToString());
            }
        }
 
        private void Fire() 
        {
            // танки противника стреляют постоянно, игрок управляется пользователем;
            // танк не может сделать новый выстрел, пока не уничтожен предыдущий снаряд;
            // скорострельность танка определяется его типом;

            var shoots = Active(enemies).Concat(Active(players).Where(t => isPressed[VirtualKey.Space]))
                .Where(t => bullets.All(b => b.Tank != t))
                .Where(t => t.FireDelay + t.LastFireFrame <= currentFrame);

            foreach (var tank in shoots)
            {
                var bullet = tank.Fire();
                bullet.X = bullet.Direction == Direction.Left ? tank.X : bullet.Direction == Direction.Right ? (tank.X + tank.Width - bullet.Width) : (tank.X + tank.Width / 2 - bullet.Width / 2);
                bullet.Y = bullet.Direction == Direction.Down ? tank.Y : bullet.Direction == Direction.Up ? (tank.Y + tank.Height - bullet.Height) : (tank.Y + tank.Height / 2 - bullet.Height / 2);
                bullets.Add(bullet);
                Play(Sound.Shoot);
                tank.LastFireFrame = currentFrame;
            }
        }

        private void CheckGameState()
        {
            // для визуальной паузы обновляем состояние игры с определенной
            // задержкой относительно последнего уничтожения танка

            if (currentFrame >= (lastGamplayDelayFrame + gameplayDelay))
            {
                // противники появляются в случайной позиции в верхней части игрового поля,
                // игроки - в нижней части

                Func<ITank, int> x = t => t.Width * rn.Next(0, fieldSize / t.Width);

                Enable(enemiesConcurrentCount, enemies, Direction.Down, x, t => fieldSize - t.Height);
                Enable(1, players, Direction.Up, x, t => 0);

                // состояние игры зависит от текущего количества игроков и противников

                State =
                    players.Count == 0 ? GameState.Lost : 
                    enemies.Count == 0 ? GameState.Won : 
                    GameState.Running;
            }
        }

        private void Disable(IEnumerable<ITank> source)
        {
            foreach (var t in source)
            {
                t.IsDisabled = true;
                t.LastFireFrame = 0;
            }
        }

        private void Enable(int count, IEnumerable<ITank> source, Direction direction, Func<ITank, int> x, Func<ITank, int> y)
        {
            for (int i = 0; i < count - Active(source).Count(); i++)
            {
                var t = source.FirstOrDefault(obj => obj.IsDisabled);
                if (t == null)
                {
                    break;
                }

                t.Direction = direction;
                t.X = x(t);
                t.Y = y(t);

                // если выбранная позиция занята, то попытка добавить танк
                // откладывается до следующей итерации обновления геймплея

                if (!Enumerable.Concat<IGameObject>(Active(players), Active(enemies)).Concat(miscellaneous).Any(obj => HasCollision(obj, t)))
                {
                    t.IsDisabled = false;
                }
            }
        }

        private void Requires(GameState state)
        {
            if (State != state)
            {
                throw new InvalidOperationException(string.Format("{0} is required.", state));
            }
        }

        private async Task UpdateAnimationFrame()
        {
            currentFrame++;

            AnimateExplosions();
            MoveTanks();
            Fire();
            MoveBulletsAndCheckHits();
            CheckGameState();

            if (State == GameState.Won)
            {
                try
                {
                    await ApplicationData.Current.LocalFolder.GetFileAsync(string.Format(levelFile, currentLevelNumber + 1));
                }
                catch (FileNotFoundException)
                {
                    State = GameState.Completed;
                }
            }
        }

        private async Task Start()
        {
            Clear();

            // новую игру инициализируем параметрами по умолчанию:
            // у игрока 2 жизни в запасе

            for (int i = 0; i < 2; i++)
            {
                players.Add(new Tank("player", "playerBullet"));
            }

            // данные уровней загружаются асинхронно

            await Load(1);

            // для визуальной стартовой паузы переводим все танки в "запас" 

            Disable(players);
            Disable(enemies);

            State = GameState.Running;
        }

        private async Task<bool> Continue()
        {
            Clear(false);

            try
            {
                await Load(CurrentLevelNumber + 1);
            }
            catch (FileNotFoundException)
            {
                return false;
            }

            // для визуальной стартовой паузы переводим все танки в "запас" 

            Disable(players);
            Disable(enemies);

            State = GameState.Running;

            // в начале каждого уровня выполняем autosave

            await Suspend();

            return true;
        }

        private async Task Suspend()
        {
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(suspendedFile, CreationCollisionOption.ReplaceExisting);
            var data = JsonConvert.SerializeObject(this, jsonSettings);

            await FileIO.WriteTextAsync(file, data);

            CanResume = true;
        }

        private async Task Resume()
        {
            var file = await ApplicationData.Current.LocalFolder.GetFileAsync(suspendedFile);
            var data = await FileIO.ReadTextAsync(file);
            var game = JsonConvert.DeserializeObject<Game>(data, jsonSettings);

            Clear();

            players = game.players;
            enemies = game.enemies;
            miscellaneous = game.miscellaneous;
            bullets = game.bullets;
            explosions = game.explosions;
            currentLevelNumber = game.currentLevelNumber;

            // задержки считаем от текущего кадра

            currentFrame = lastGamplayDelayFrame = game.currentFrame;

            State = GameState.Running;
        }

        private async Task Construct()
        {
            Clear();

            State = GameState.Constructing;

            await Load(1);
        }

        private async Task Load(int levelNumber)
        {
            if (levelNumber <= 0)
            {
                throw new ArgumentOutOfRangeException("levelNumber", "Must be greater then 0");
            }

            try
            {
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync(string.Format(levelFile, levelNumber));
                var levelJsonData = await FileIO.ReadTextAsync(file);
                var level = JsonConvert.DeserializeObject<GameLevel>(levelJsonData, jsonSettings);

                enemies = level.Enemies;
                miscellaneous = level.Miscellaneous;
            }
            catch (FileNotFoundException)
            {
                if (State != GameState.Constructing)
                {
                    throw;
                }
                else
                {
                    Clear();
                }
            }

            currentLevelNumber = levelNumber;
        }

        private async Task<string> Save()
        {
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(string.Format(levelFile, CurrentLevelNumber), CreationCollisionOption.ReplaceExisting);
            var json = JsonConvert.SerializeObject(new GameLevel { Enemies = enemies, Miscellaneous = miscellaneous }, Formatting.Indented, jsonSettings);

            await FileIO.WriteTextAsync(file, json);
#if DEBUG
            return json;
#else
            return null;
#endif
        }
         
        #endregion

        /// <summary>
        /// Инициализирует экземпляр класса <see cref="Game"/>.
        /// </summary>
        /// <returns></returns>
        public static IAsyncOperation<Game> InitAsync()
        {
            return Init().AsAsyncOperation();
        }

        /// <summary>
        /// Возвращает <c>true</c>, если доступно возобновление прошлой игры, 
        /// <c>false</c>, если недоступно, и <c>null</c>, если значение еще неизвестно.
        /// </summary>
        [JsonIgnore]
        public bool? CanResume
        {
            get;
            private set;
        }

        /// <summary>
        /// Возвращает номер текущего уровня.
        /// </summary>
        public int CurrentLevelNumber
        {
            get { return currentLevelNumber; }
        }

        /// <summary>
        /// Набор всех объектов, отображаемых на игровом поле.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<IGameObject> Scene 
        {
            get
            {
                return miscellaneous
                    .Concat<IGameObject>(players)
                    .Concat<IGameObject>(enemies)
                    .Concat<IGameObject>(bullets)
                    .Concat<IGameObject>(explosions)
                    .ToList();
            }
        }

        /// <summary>
        /// Возвращает состояние игры.
        /// </summary>
        public GameState State
        {
            get;
            private set;
        }

        /// <summary>
        /// Устанавливает состояние клавиш.
        /// </summary>
        /// <remarks>
        /// Для предотвращения гонок нажатия фиксируются синхронно,
        /// иначе флаг нажатия может быть сброшен раньше, чем установлен.
        /// </remarks>
        /// <param name="key">Код нажатой/отпущенной клавиши.</param>
        /// <param name="pressed">Флаг нажатия.</param>
        public void SetKey(VirtualKey key, bool pressed)
        {
            Requires(GameState.Running);

            if (isPressed.ContainsKey(key))
            {
                isPressed[key] = pressed;
            }
        }

        /// <summary>
        /// Обновляет состояние игры, рассчитывает очередной кадр.
        /// </summary>
        public IAsyncAction UpdateAnimationFrameAsync()
        {
            Requires(GameState.Running);

            return UpdateAnimationFrame().AsAsyncAction();
        }

        /// <summary>
        /// Инициализирует новую игру с первого уровня.
        /// </summary>
        /// <returns></returns>
        public IAsyncAction StartAsync()
        {
            return Start().AsAsyncAction();
        }

        /// <summary>
        /// Возвращает <c>true</c>, если удалось загрузить следующий уровень, иначе - <c>false</c>, игра пройдена до конца.
        /// </summary>
        /// <returns></returns>
        public IAsyncOperation<bool> ContinueAsync()
        {
            return Continue().AsAsyncOperation();
        }

        /// <summary>
        /// Приостанавливает игру, записывает состояние на диск.
        /// </summary>
        /// <returns></returns>
        public IAsyncAction SuspendAsync()
        {
            Requires(GameState.Running);

            return Suspend().AsAsyncAction();
        }

        /// <summary>
        /// Восстанавливает состояние игры, записанное прежде с помощью метода <see cref="SuspendAsync"/>.
        /// </summary>
        /// <returns></returns>
        public IAsyncAction ResumeAsync()
        {
            return Resume().AsAsyncAction();
        }

        public IAsyncAction ConstructAsync()
        {
            return Construct().AsAsyncAction();   
        }

        public IAsyncAction LoadAsync(int levelNumber)
        {
            Requires(GameState.Constructing);

            return Load(levelNumber).AsAsyncAction();
        }

        public IAsyncOperation<string> SaveAsync()
        {
            Requires(GameState.Constructing);

            return Save().AsAsyncOperation();
        }

        public void ExchangeBlocks(int x, int y)
        {
            Requires(GameState.Constructing);

            var blockX = (int)Math.Floor((decimal)x / fieldSizeModule) * fieldSizeModule;
            var blockY = (int)Math.Floor((decimal)y / fieldSizeModule) * fieldSizeModule;

            IMiscellaneous curr = miscellaneous.FirstOrDefault(e => e.X == blockX && e.Y == blockY);
            IMiscellaneous next =
                curr == null ? new Bricks() :
                curr is Bricks ? new Concrete() :
                curr is Concrete ? new Water() :
                default(IMiscellaneous);

            if (curr != null)
            {
                miscellaneous.Remove(curr);
            }

            if (next != null)
            {
                next.X = blockX;
                next.Y = blockY;

                miscellaneous.Add(next);
            }
        }

        public void InsertEnemy()
        {
            Requires(GameState.Constructing);

            enemies.Add(new Tank("enemy", "enemyBullet"));
        }

        public void RemoveEnemy()
        {
            Requires(GameState.Constructing);

            if (enemies.Count > 0)
            {
                enemies.RemoveAt(enemies.Count - 1);
            }
        }
    }
}
