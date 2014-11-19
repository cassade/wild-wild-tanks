 angular
    .module('tanks', ['ngAnimate', 'ui.router', 'winjs'])
    .config(function ($stateProvider, $urlRouterProvider) {

        function registerState() {
            var name = arguments[arguments.length - 1];
            var root = arguments[0];
            $stateProvider.state(arguments.length > 1 ? root + '.' + name : name, {
                url: '/' + name,
                templateUrl: 'views/' + name + '.html',
            })
        }

        $stateProvider
            .state('home', {
                abstract: true,
                url: '/home',
                templateUrl: 'views/home.html',
                controller: 'home',
            })
            .state('game', {
                url: '/game',
                templateUrl: 'views/game.html',
                controller: 'game',
            });

        registerState('home', 'completed');
        registerState('home', 'fail');
        registerState('home', 'lost');
        registerState('home', 'start');
        registerState('home', 'won');

        $urlRouterProvider.otherwise("/home/start");

    })
    .run(function ($rootScope, $state) {

        $rootScope.SOUND_HAVE_ENOUGH_DATA = 4;
        $rootScope.GAME_RUNNING = 0;
        $rootScope.GAME_LOST = 1;
        $rootScope.GAME_WON = 2;
        $rootScope.GAME_COMPLETED = 3;
        $rootScope.GAME_CONSTRUCTING = 4;
        $rootScope.game;
        $rootScope.images = {};
        $rootScope.sounds = {};
        $rootScope.onFail = function (data) {
            $state.go('home.fail', data);
        };

        // настройка и запуск приложения WinJS, взаимодействие с окружением

        WinJS.Application.onsettings = function (args) {
            args.detail.applicationcommands = {
                'about': { title: 'About', href: '/views/about.html' }
            };
            WinJS.UI.SettingsFlyout.populateSettings(args);
        }; 

        WinJS.Application.oncheckpoint = function (args) {

            // при выходе из приложения во время игры сохраняем её состояние на диск

            if ($rootScope.game.state === $rootScope.GAME_RUNNING) {
                args.setPromise($rootScope.game.suspendAsync());
            }
        };

        WinJS.Application.start();

        // сихнронизируем навигацию Angular UI Router и WinJS

        $rootScope.$on('$stateChangeSuccess', function (event, toState, toParams, fromState, fromParams) {
            WinJS.Navigation.navigate(toState.name, toParams);
        });

        WinJS.Navigation.onnavigated = function (e) {
            if ($state.is(e.detail.location, e.detail.state) == false) {
                $state.go(e.detail.location, e.detail.state);
            }
        }

        // проигрывание звуков/мелодий

        function play(sound) {
            if ($rootScope.sounds[sound].readyState >= $rootScope.SOUND_HAVE_ENOUGH_DATA) {
                $rootScope.sounds[sound].currentTime = 0;
                $rootScope.sounds[sound].play();
            }
        }

        // инициализация игры, загрузка ресурсов (спрайтов/звуков)

        function onInit(game) {

            // инициализируем текущий экземпляр игры

            $rootScope.game = game;
        };

        function doLoad() {

            var promises = {};

            promises.images = Windows.ApplicationModel.Package.current.installedLocation.getFolderAsync('resources\\images')
                .then(function (fold) {

                    // будем загружать все файлы каталога

                    return fold.getFilesAsync();
                })
                .then(function (list) {

                    // последовательно загружаем все изображения из каталога спрайтов,
                    // после чего создаем новый экземпляр игры

                    return new WinJS.Promise(function (onComplete, onError, onProgress) {
                        var loaded = 0;
                        for (var i = 0; i < list.length; i++) {
                            $rootScope.images[list[i].displayName] = new Image();
                            $rootScope.images[list[i].displayName].src = 'ms-appx:///resources/images/' + list[i].name;
                            $rootScope.images[list[i].displayName].addEventListener('load', function () {
                                if (++loaded >= list.length) {
                                    onComplete();
                                }
                            });
                        }
                    });
                });

            promises.sounds = Windows.ApplicationModel.Package.current.installedLocation.getFolderAsync('resources\\sounds')
                .then(function (fold) {

                    // будем загружать все файлы каталога

                    return fold.getFilesAsync();
                })
                .then(function (list) {

                    // только после загрузки медиафайла его можно безопасно проигрывать;
                    // последовательно загружаем все файлы из каталога мелодий;

                    return new WinJS.Promise(function (onComplete, onError, onProgress) {
                        var sounds = {};
                        var loaded = 0;
                        for (var i = 0; i < list.length; i++) {
                            $rootScope.sounds[list[i].displayName] = new Audio('ms-appx:///resources/sounds/' + list[i].name);
                            $rootScope.sounds[list[i].displayName].addEventListener('canplaythrough', function () {
                                if (++loaded >= list.length) {
                                    onComplete();
                                }
                            });
                        }
                    });
                })
                .then(function () {

                    // начинаем обработку запросов на проигрывание звуков только после загрузки всех медиафайлов;

                    $rootScope.game.onsoundrequested = play;
                });

            return WinJS.Promise.join(promises);
        };

        function onLoad() {

            // уведомляем вложенные контроллеры об окончании загрузки ресурсов

            $rootScope.initialized = true;
        };

        function onDone() {

            // $digest() нужно вызвать вручную, так как использовались WinJS, а не AngularJS promises;

            $rootScope.$digest();
        };

        // инициализация

        Tanks.Core.Game.initAsync()
            .then(onInit)
            .then(doLoad)
            .then(onLoad)
            .then(null, $rootScope.onFail)
            .done(onDone);
    });

