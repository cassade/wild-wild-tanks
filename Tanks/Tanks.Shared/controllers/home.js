angular
    .module('tanks')
    .controller('home', ['$scope', '$state', function ($scope, $state) {

        function play() {
            $state.go('game');
        };

        function playIfNextLevelExists(exists) {
            $state.go(exists ? 'game' : 'home.won');
        }

        $scope.startNew = function () {
            $scope.game.startAsync()
                .then(play, $scope.onFail);
        };

        $scope.resumePrevious = function () {
            $scope.game.resumeAsync()
                .then(play, $scope.onFail);
        };

        $scope.continue = function () {
            $scope.game.continueAsync()
                .then(playIfNextLevelExists, $scope.onFail);
        };

        $scope.constructLevel = function () {
            $scope.game.constructAsync()
                .then(play, $scope.onFail);
        };

        $scope.back = function () {
            $state.go('home.start');
        };

        $scope.showBackButton = function () {
            return !$state.is('home.start');
        };

    }]);