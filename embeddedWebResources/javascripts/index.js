/*
Copyright 2016 Cinegy GmbH

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

/*Global variables*/
$(document).ready(function () {

    var ViewModel = function () {
        var self = this;

        //Wire up methods that are exposed via KnockOut
        self.doMagic = function () {
            onDoMagicHandler(self);
        };
        
    };

    var viewModelObj = new ViewModel();
    ko.applyBindings(viewModelObj);

    function onDoMagicHandler(engineViewModel, buttonIds) {
        alert("Abracadabra");
    }

});