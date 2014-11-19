angular
    .module('tanks')
    .controller('game', ['$scope', '$rootScope', '$state', function ($scope, $rootScope, $state) {

        var TO_RADIANS = Math.PI / 180;
        var now = new Date().getTime();
        var fps = 0;
        var context = document.getElementById('game-canvas').getContext('2d');

        $scope.onKeyUpDown = function ($event) {
            if ($scope.game.state == $scope.GAME_RUNNING) {
                $scope.game.setKey($event.keyCode, $event.type == 'keydown');
            }
        };

        $scope.onMouseDown = function ($event) {
            if ($scope.game.state == $scope.GAME_CONSTRUCTING) {
                $scope.game.exchangeBlocks($event.offsetX, context.canvas.height - $event.offsetY);
            }
        };
        
        $scope.isPaused = false;

        $scope.pause = function () {

            $scope.isPaused = true;

            // приостановка игры приводит к промежуточному сохранению игры,
            // делаем это в фоне чтобы обеспечить отзывчивый UI,
            // после чего broadcast'им digest через корневой scope

            if ($scope.game.state == $scope.GAME_RUNNING) {
                $scope.game.suspendAsync()
                    .then(function () {
                        $rootScope.$digest();
                    });
            }
        }

        $scope.break = function () {

            // при возврате запускаем сохранение игры в фоне,
            // и, не дожидаясь его окончания, переходим к основному экрану

            $scope.pause();
            $state.go('home.start');
        };

        $scope.start = function () {

            // для корректного перехвата нажатий клавиш корневой HTML-элемент должен быть в фокусе

            document.getElementById('game-container').focus();

            // сбрасываем флаг приостановки игры

            $scope.isPaused = false;

            // запускаем основной игровой цикл,
            // определяя реакцию на запрос фрейма анимации

            requestAnimationFrame(animate);
        }

        $scope.getPlayers = function () {
            return (
                $scope.game != null ?
                $scope.game.scene.filter(function (item) { return item.resourceId == 'player'; }) :
                null
            )
        };

        $scope.getEnemies = function () {
            return (
                $scope.game != null ?
                $scope.game.scene.filter(function (item) { return item.resourceId == 'enemy'; }) :
                null
            )
        };

        $scope.nextLevel = function() {
            $scope.game.loadAsync($scope.game.currentLevelNumber + 1);
        }

        $scope.prevLevel = function () {
            $scope.game.loadAsync($scope.game.currentLevelNumber - 1);
        }

        $scope.save = function () {
            $scope.game.saveAsync().then(function (levelContent) {
                if (levelContent) {
                    var savePicker = new Windows.Storage.Pickers.FileSavePicker();
                    savePicker.suggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.documentsLibrary;
                    savePicker.fileTypeChoices.insert('json', ['.json']);
                    savePicker.suggestedFileName = 'level' + $scope.game.currentLevelNumber;
                    savePicker.pickSaveFileAsync().then(function (file) {
                        if (file) {
                            Windows.Storage.FileIO.writeTextAsync(file, levelContent);
                        }
                    });
                }
            });
        };

        function invertY(obj) {
            return context.canvas.height - obj.y - obj.height;
        };

        function draw() {

            context.clearRect(0, 0, context.canvas.width, context.canvas.height)

            for (var i = 0; i < $scope.game.scene.size; i++) {
                var obj = $scope.game.scene.getAt(i);
                if (obj.isDisabled) {
                    continue;
                }
                if (obj.direction > 0) {
                    context.save();
                    context.translate(obj.x + obj.width / 2, invertY(obj) + obj.height / 2);
                    context.rotate(obj.direction * TO_RADIANS);
                    context.drawImage($scope.images[obj.resourceId + obj.frame], -obj.width / 2, -obj.height / 2);
                    context.restore();
                }
                else {
                    context.drawImage($scope.images[obj.resourceId + obj.frame], obj.x, invertY(obj));
                }
            }
        }

        function calcFps() {
            if (new Date().getTime() - now >= 1000) {
                $scope.fps = fps;
                fps = 0;
                now = new Date().getTime();
            };
            fps++;
        };

        function refresh() {
            calcFps();
            draw();
            requestAnimationFrame(animate);
            $scope.$digest();
        };

        function animate() {

            // если игра приостановлена или окончен текущий уровень,
            // то останавливаем игровой цикл

            if ($scope.isPaused) {
                return;
            }

            if ($scope.game.state == $scope.GAME_WON) {
                $state.go('home.won');
                return;
            }

            if ($scope.game.state == $scope.GAME_COMPLETED) {
                $state.go('home.completed');
                return;
            }

            if ($scope.game.state == $scope.GAME_LOST) {
                $state.go('home.lost');
                return;
            }

            // в режиме конструирования обновлять состояние игры не нужно (и ядро выбросит исключение),
            // требуется только перерисовка всех игровых объектов

            if ($scope.game.state == $scope.GAME_RUNNING) {
                $scope.game.updateAnimationFrameAsync().then(refresh);
            }
            else {
                refresh();
            }
        };

        // при переходе со страницы активной игры с помощью аппаратной кнопки BACK сохраняем состояние игры

        WinJS.Application.onbackclick = function (e) {
            $scope.pause();
        };

        // для масштабирования используется WinJS.UI.ViewBox,
        // для его работы требуется запустить обработку разметки UI движком WinJS

        WinJS.UI.processAll();

        // запускаем основной игровой цикл

        $scope.start();

    }]);