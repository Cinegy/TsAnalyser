# Cinegy TS Analyser Tool

Use this tool to view inbound network, RTP and TS packet details. Use newly introduced powers to view into the service description tables, and even decode a teletext stream!

New with V3 - now built with Net Core 3, using single-file-exe features with an integrated runtime, making deployment dependencies incredibly low! This also means that operation on Linux and MacOS are possible (although should be considered beta - it is not particularly tested).

## How easy is it?

Well, we've added everything you need into a single EXE again, which holds the Net Core 3.1 runtime - so if you have the basics installed on a machine, you should be pretty much good to go. We gave it all a nice Apache license, so you can tinker and throw the tool wherever you need to on the planet.

Just run the EXE from inside a command-prompt, and you will be offered a basic interactive mode to get cracking checking out your stream.

From v1.3, you can check out a .TS file and scan through it - just drag / drop the .TS file onto the EXE!

You can print live Teletext decoding, and you can use the tool to generate input logs for 'big data' analysis (which is very cool).

## Command line arguments:

Double click, or just run without (or with incorrect) arguments, and you'll see this:

```
TsAnalyser 3.0.198
Cinegy GmbH

ERROR(S):
  No verb selected.

  stream     Stream from the network.

  read       Read from a file.

  help       Display more information on a specific command.

  version    Display version information.

```

The help details for the 'stream' verb look like this:

```
c:\> TsAnalyser.exe  help stream      
                                                         
TsAnalyser 3.0.198
Cinegy GmbH

  -m, --multicastaddress             Input multicast address to read from - if left blank, assumes unicast.

  -p, --port                         Required. Input UDP network port to read from.

  -a, --adapter                      IP address of the adapter to listen for multicasts (if not set, tries first binding adapter).

  -n, --nortpheaders                 (Default: false) Optional instruction to skip the expected 12 byte RTP headers (meaning plain
                                     MPEGTS inside UDP is expected

  -i, --interarrivaltime             (Default: 40) Maximum permitted time between UDP packets before alarming.

  -h, --savehistoricaldata           (Default: false) Optional instruction to save and then flush to disk recent TS data on stream
                                     problems.

  -e, --timeserieslogging            Record time slice metric data to log file.

  -q, --quiet                        (Default: false) Don't print anything to the console

  -l, --logfile                      Optional file to record events to.

  -s, --skipdecodetransportstream    (Default: false) Optional instruction to skip decoding further TS and DVB data and metadata

  -c, --teletextdecode               (Default: false) Optional instruction to decode DVB teletext subtitles / captions from default
                                     program

  --programnumber                    Pick a specific program / service to inspect (otherwise picks default).

  -d, --descriptortags               (Default: ) Comma separated tag values added to all log entries for instance and machine
                                     identification

  -v, --verboselogging               Creates event logs for all discontinuities and skips.

  -t, --telemetry                    (Default: false) Enable telemetry to Cinegy Telemetry Server

  -o, --organization                 Tag all telemetry with this organization (needed to indentify and access telemetry from Cinegy
                                     Analytics portal

  --help                             Display this help screen.

  --version                          Display version information.

```

So - what does this look like when you point it at a complex live stream? Here is a shot from a UK DVB-T2 stream:

```
URL: rtp://@239.5.2.1:6670      Running time: 00:00:08

Network Details
----------------
Total Packets Rcvd: 35422       Buffer Usage: 0.09%/(Peak: 0.68%)
Total Data (MB): 44             Packets per sec:4229
Period Max Packet Jitter (ms): 6
Bitrates (Mbps): 42.85/42.85/42.93/0.00 (Current/Avg/Peak/Low)

RTP Details
----------------
Seq Num: 4348   Min Lost Pkts: 0
Timestamp: 2186342583   SSRC: 0

PCR Value: 09:38:03.4968494
----------------

PID Details - Unique PIDs: 59, (10 shown by packet count)
----------------
TS PID: 8191    Packet Count: 105577            CC Error Count: 0
TS PID: 5500    Packet Count: 36759             CC Error Count: 0
TS PID: 5600    Packet Count: 31223             CC Error Count: 0
TS PID: 5400    Packet Count: 19812             CC Error Count: 0
TS PID: 5300    Packet Count: 18008             CC Error Count: 0
TS PID: 2322    Packet Count: 8354              CC Error Count: 0
TS PID: 3847    Packet Count: 3899              CC Error Count: 0
TS PID: 2321    Packet Count: 2784              CC Error Count: 0
TS PID: 192     Packet Count: 1913              CC Error Count: 0
TS PID: 3843    Packet Count: 1710              CC Error Count: 0

Service Information - Service Count: 9, (5 shown)
----------------
Service 6940: BBC Two HD (BSkyB) - H.264/AVC HD digital television service
Service 6941: BBC One HD (BSkyB) - H.264/AVC HD digital television service
Service 6943: BBC One NI HD (BSkyB) - H.264/AVC HD digital television service
Service 6945: 6945 (BSkyB) - H.264/AVC HD digital television service
Service 6952: CBBC HD (BSkyB) - H.264/AVC HD digital television service

Elements - Selected Program: BBC Two HD (ID:6940) (first 5 shown)
----------------
PID: 5500 (H.264 video)
PID: 5502 (MPEG-1 audio)
PID: 5504 (MPEG-2 packetized data privately defined)
PID: 5503 (MPEG-2 packetized data privately defined)
PID: 5501 (MPEG-2 packetized data privately defined)

Teletext Subtitles (eng)- decoding from Service ID 6940, PID: 5503
Total Pages: 2, Total Clears: 1
----------------
Live Decoding Page 8136

       shown by young lads
    who had acted as lookouts
   and helped guard prisoners.

```

Just to make your life easier, we auto-build this using AppVeyor - here is how we are doing right now: 

[![Build status](https://ci.appveyor.com/api/projects/status/08dqscip26lr0g1o/branch/master?svg=true)](https://ci.appveyor.com/project/cinegy/tsanalyser/branch/master)

You can check out the latest compiled binary from the master or pre-master code here:

[AppVeyor TSAnalyser Project Builder](https://ci.appveyor.com/project/cinegy/tsanalyser/build/artifacts)

