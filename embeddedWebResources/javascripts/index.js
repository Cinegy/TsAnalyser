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
        self.Network = {
            AverageBitrate : ko.observable(''),
            CurrentBitrate : ko.observable(''),
            HighestBitrate : ko.observable(''),
            LongestTimeBetweenPackets : ko.observable(''),
            LowestBitrate : ko.observable(''),
            NetworkBufferUsage : ko.observable(''),
            PacketsPerSecond : ko.observable(''),
            ShortestTimeBetweenPackets : ko.observable(''),
            TimeBetweenLastPacket: ko.observable(''),
            TotalPacketsRecieved: ko.observable('')
        };
        self.Rtp = {
            MinLostPackets: ko.observable(''),
            SSRC: ko.observable(''),
            SequenceNumber: ko.observable(''),
            Timestamp: ko.observable('')
        };
        self.Service = {
            ServiceName: ko.observable(''),
            ServiceProvider: ko.observable('')
        }

        self.Ts = {
            Pids : ko.observableArray()
        }
        
        //Wire up methods that are exposed via KnockOut
        self.doMagic = function () {
            onDoMagicHandler(self);
        };
        
        self.UpdateValues = function (values) {
            //self.Network = values.Network;
            self.Network.AverageBitrate(values.Network.AverageBitrate);
            self.Network.CurrentBitrate(values.Network.CurrentBitrate);
            self.Network.HighestBitrate(values.Network.HighestBitrate);
            self.Network.LongestTimeBetweenPackets(values.Network.LongestTimeBetweenPackets);
            self.Network.LowestBitrate(values.Network.LowestBitrate);
            self.Network.NetworkBufferUsage(values.Network.NetworkBufferUsage);
            self.Network.PacketsPerSecond(values.Network.PacketsPerSecond);
            self.Network.ShortestTimeBetweenPackets(values.Network.ShortestTimeBetweenPackets);
            self.Network.TimeBetweenLastPacket(values.Network.TimeBetweenLastPacket);
            self.Network.TotalPacketsRecieved(values.Network.TotalPacketsRecieved);

            self.Rtp.MinLostPackets(values.Rtp.MinLostPackets);
            self.Rtp.SSRC(values.Rtp.SSRC);
            self.Rtp.SequenceNumber(values.Rtp.SequenceNumber);
            self.Rtp.Timestamp(values.Rtp.Timestamp);

            self.Service.ServiceName(values.Service.ServiceName);
            self.Service.ServiceProvider(values.Service.ServiceProvider);

            self.Ts.Pids.removeAll();
            $.each(values.Ts.Pids, function(k,v){
                self.Ts.Pids.push(v);
            });
        };
    };

    var viewModelObj = new ViewModel();
    ko.applyBindings(viewModelObj);

    function onDoMagicHandler(engineViewModel, buttonIds) {
        alert("Abracadabra");
    }
    setInterval(function () {
        $.getJSON("/Analyser/V1/CurrentMetrics", function (data) { viewModelObj.UpdateValues(data); });
        //viewModelObj.doMagic();
    }, 500);
});