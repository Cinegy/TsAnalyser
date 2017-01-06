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
            TotalPacketsReceived: ko.observable('')
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
        };

        self.Ts = {
            Pids: ko.observableArray()
        };
        
        //Wire up methods that are exposed via KnockOut
        self.resetMetrics = function () {
            onResetMetricsHandler(self);
        };
        
        self.UpdateValues = function (values) {
            //self.Network = values.Network;
            self.Network.AverageBitrate((Math.round(values.AverageBitrate / 10485.76)) / 100);
            self.Network.CurrentBitrate((Math.round(values.CurrentBitrate / 10485.76)) / 100);
            self.Network.HighestBitrate((Math.round(values.HighestBitrate / 10485.76)) / 100);
            self.Network.LongestTimeBetweenPackets(values.PeriodLongestTimeBetweenPackets);
            self.Network.LowestBitrate((Math.round(values.LowestBitrate / 10485.76)) / 100);
            self.Network.NetworkBufferUsage(Math.round(values.PeriodMaxNetworkBufferUsage * 100)/100);
            self.Network.PacketsPerSecond(values.PacketsPerSecond);
            self.Network.ShortestTimeBetweenPackets(values.PeriodShortestTimeBetweenPackets);
            self.Network.TotalPacketsReceived(values.TotalPackets);

            //self.Rtp.EstimatedLostPackets(values.Rtp.EstimatedLostPackets);
            //self.Rtp.SSRC(values.Rtp.SSRC);
            //self.Rtp.SequenceNumber(values.Rtp.SequenceNumber);
            //self.Rtp.Timestamp(values.Rtp.Timestamp);

            //self.Service.ServiceName(values.Service.ServiceName);
            //self.Service.ServiceProvider(values.Service.ServiceProvider);

            //self.Ts.Pids.removeAll();
            //$.each(values.Ts.Pids, function(k,v){
            //    self.Ts.Pids.push(v);
            //});
        };
    };

    var viewModelObj = new ViewModel();
    ko.applyBindings(viewModelObj);

    function onResetMetricsHandler(analyserViewModel) {
        issueCommand(analyserViewModel, "ResetMetrics");
    }

    setInterval(function () {
        $.getJSON("/V1/NetworkMetric", function (data) { viewModelObj.UpdateValues(data); });
    }, 800);


    function issueCommand(engineViewModel, command) {

        var url = "v1/" + command;
        $.ajax({
            url: url,
            type: "POST",
            dataType: 'xml',
            contentType: "text/xml; charset=utf-8"
        });
    }
});