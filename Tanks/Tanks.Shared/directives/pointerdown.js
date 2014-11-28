angular.module('tanks')
    .directive('pointerdown', function ($parse) {
        return {
            restrict: 'A',
            link: function ($scope, element, attrs) {
                var handler = $parse(attrs.pointerdown);
                element[0].addEventListener('pointerdown', function (e) {
                    handler($scope, {
                        $event: e
                    });
                });
            },
        };
    });