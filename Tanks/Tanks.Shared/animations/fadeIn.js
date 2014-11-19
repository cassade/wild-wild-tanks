angular
    .module('tanks')
    .animation('.fade-in-animation', function () {

        function animate(elements, done) {
            if (WinJS.UI.Animation.fadeIn) {
                WinJS.UI.Animation.fadeIn(elements[0]).then(done);
            }
            else {
                done();
            }
        };

        return {
            enter: function (elements, done) {
                animate(elements, done);
            },
            leave: function (elements, done) {
                done();
            },
            removeClass: function (elements, className, done) {
                if (className == 'ng-hide') {
                    animate(elements, done);
                }
                else {
                    done();
                }
            },
        };
    });