angular.module('tanks')
    .directive('pointerup', function ($parse) {
        return {
            restrict: 'A',
            link: function ($scope, element, attrs) {
                var targetHandler = $parse(attrs.pointerup);
                var sourceHandler = function (e) {
                    targetHandler($scope, {
                        $event: e
                    });
                };
                element[0].addEventListener('pointerup' , sourceHandler);
                element[0].addEventListener('pointerout', sourceHandler);
            },
        };
    });