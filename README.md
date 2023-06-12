# Cinegy TS Analyzer Tool

Use this tool to view inbound network, RTP, SRT and plain UDP TS packet details. 

Linux builds, for Intel/AMD 64-bit are now created by the AppVeyor build but the general expectation for use on Linux would be to be running inside a Docker Container!

## How easy is it?

Well, we've added everything you need into a single EXE again, which holds the Net Core 3.1 runtime - so if you have the basics installed on a machine, you should be pretty much good to go. We gave it all a nice Apache license, so you can tinker and throw the tool wherever you need to on the planet.

Just run the EXE from inside a command-prompt, and you will be offered a basic interactive mode to get cracking checking out your stream.

From v1.3, you can check out a .TS file and scan through it - just drag / drop the .TS file onto the EXE!

Starting with V4, there is a Dockerfile, so you can build your own container and use it like so:

`
docker build . -t mylocaltsanalyzer:latest -f .\Cinegy.TsAnalyzer\Dockerfile
`

You can then run it with something like this:

`
docker run --rm -it docker.io/library/tsanalyzer --sourceurl=srt://srt.cinegy.com:6000
`

## Command line arguments:

With the migration to containers, the magic command-line arguments auto-documentation got broken - but we cleaned up the logic to encapsulate a more 'URL' style model instead. You can set up an appsettings.json file, set environment VARS, or inject command-line arguments like so:

Argument:
```
--sourceurl=srt://srt.cinegy.com:9000
```

ENV VAR:
```
CINEGYTSA_SourceURL=srt://srt.cinegy.com:9000
```

appsettings.json:
```
{
  "sourceUrl": "srt://srt.cinegy.com:9000"
}
```

Here is the list of settable parameters (in command-line-args style):

```
//Core settings
--ident="Analyzer1" //value tagged to core metric, which can be used in statistics aggregation and identification
--label="Cinegy Test Stream" //value tagged to core metric, which can be used in statistics aggregation and identification
--sourceUrl="srt://srt.cinegy.com:9000" //URL format of source - supports srt, rtp and udp schemes with optional source-specific formatting
--liveConsole=true //note - this is only supported on Windows, since more aggressive Console.Write operations fail on Linux

//Metrics-related settings
--metrics:enabled=false //default is true
--metrics:consoleExporterEnabled=true //default is false - used to enable console output of OTEL metrics
--metrics:openTelemetryExporterEnabled=true //default is false - used to enable exporting of OTEL metrics to endpoint
--metrics:openTelemetryEndpoint="http://localhost:4317" //default is that localhost value - used to specify OTEL collection endpoint URL
--metrics:openTelemetryPeriodicExportInterval=1000 //in milliseconds, default is 10 seconds - used to control frequency of data samples pushed via OTEL

```

To read more about how these settings can work, and how to transform console arguments to JSON or ENV vars, see the MS documentation here:

https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration


So - what does this look like when you point it at a complex live stream? Here is a shot from a UK DVB-T2 stream:

```
Network Details - srt://srt.cinegy.com:9000             Running: 00:00:01
---------------------------------------------------------------------
Total Packets Rcvd: 694
Total Data (MB): 0              Packets per sec:636
Period Max Packet Jitter (ms): 0.0      Corrupt TS Packets: 0
Bitrates (Mbps): 6.39/6.47/6.39/6.39 (Current/Avg/Peak/Low)
PCR Value: 21:49:38.425, OPCR Value: 00:00:00.000, Period Drift (ms): 0.00

---------------------------------------------------------------------
TS PID: 4095    Packet Count: 4443              CC Error Count: 0
TS PID: 8191    Packet Count: 265               CC Error Count: 0
TS PID: 4097    Packet Count: 98                CC Error Count: 0
TS PID: 4096    Packet Count: 32                CC Error Count: 0
TS PID: 0       Packet Count: 10                CC Error Count: 0
TS PID: 256     Packet Count: 10                CC Error Count: 0

Service Information - Service Count: 1
---------------------------------------------------------------------

Elements - Selected Program Service ID 1 (first 5 shown)
---------------------------------------------------------------------
PID: 4095 (H.264 video)
PID: 4097 (MPEG-1 audio)
```

Just to make your life easier, we auto-build this using AppVeyor - here is how we are doing right now: 

[![Build status](https://ci.appveyor.com/api/projects/status/08dqscip26lr0g1o/branch/master?svg=true)](https://ci.appveyor.com/project/cinegy/tsanalyser/branch/master)

You can check out the latest compiled binary from the master or pre-master code here:

[AppVeyor TSAnalyzer Project Builder](https://ci.appveyor.com/project/cinegy/tsanalyser/build/artifacts)

