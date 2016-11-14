#Cinegy TS Analyser Tool

Use this tool to view inbound network, RTP and TS packet details. Use newly introduce powers to view into the service description tables, and even decode a teletext stream!

##How easy is it?

Well, we've added everything you need into a single teeny-tiny EXE again, which just depends on .NET 4.5. And then we gave it all a nice Apache license, so you can tinker and throw the tool wherever you need to on the planet.

Just run the EXE from inside a command-prompt, and you will be offered a basic interactive mode to get cracking checking out your stream.

Now in v1.3, you can check out a .TS file and scan through it - just drag / drop the .TS file onto the EXE!

If you start launching things with arguments (maybe from a BAT file), try enabling the embedded web service - then you can browse to:

http://localhost:8124/index.html 

And see some realtime things displayed in your browser (great if you run the analyser headless on remote machines).

You can print live Teletext decoding, and you can use the tool to generate input logs for 'big data' analysis (which is very cool).

##Command line arguments:

Double click, or just run without (or with incorrect) arguments, and you'll see this:

```
TsAnalyser 1.3.118.0
Copyright © Cinegy GmbH 2016

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
                                                                                             
TsAnalyser 1.3.118.0                                                                                                            
Copyright © Cinegy GmbH 2016                                                                                                    
                                                                                                                                
  -m, --multicastaddress             Required. Input multicast address to read from.

  -g, --mulicastgroup                Required. Input multicast group port to read from.

  -e, --timeserieslogfile            Optional file to record time slice metric data to.

  -a, --adapter                      IP address of the adapter to listen for multicasts
                                     (if not set, tries first binding adapter).

  -n, --nortpheaders                 (Default: false) Optional instruction to skip the
                                     expected 12 byte RTP headers (meaning plain MPEGTS
                                     inside UDP is expected

  -i, --interarrivaltime             (Default: 40) Maximum permitted time between UDP
                                     packets before alarming.

  -h, --savehistoricaldata           (Default: false) Optional instruction to save and
                                     then flush to disk recent TS data on stream
                                     problems.

  -q, --quiet                        (Default: false) Don't print anything to the
                                     console

  -l, --logfile                      Optional file to record events to.

  -w, --webservices                  (Default: false) Enable Web Services (control page
                                     available on http://localhost:8124/index.html by
                                     default).

  -u, --serviceurl                   (Default: http://localhost:8124/) Optional service
                                     URL for REST web services (must change if running
                                     multiple instances with web services enabled).

  -s, --skipdecodetransportstream    (Default: false) Optional instruction to skip
                                     decoding further TS and DVB data and metadata

  -t, --teletextdecode               (Default: false) Optional instruction to decode
                                     DVB teletext subtitles from default program
                                     (experimental)

  -p, --programnumber                Pick a specific program / service to inspect
                                     (otherwise picks default).

  -d, --descriptortags               (Default: ) Comma separated tag values added to
                                     all log entries for instance and machine
                                     identification

  --help                             Display this help screen.

  --version                          Display version information.
                                                                                                                          
```

So - what does this look like when you point it at a complex live stream? Here is a shot from a UK DVB-T2 stream:

```
URL: rtp://@239.1.1.1:1234      Running time: 00:00:11

Network Details
----------------
Total Packets Rcvd: 47381       Buffer Usage: 0.00%/5
Total Data (MB): 60             Packets per sec:4232
Time Between Packets (ms): 0    Shortest/Longest: 0/3
Bitrates (Mbps): 42.92/42.86/42.98/42.83 (Current/Avg/Peak/Low)

RTP Details
----------------
Seq Num: 49141  Min Lost Pkts: 0
Timestamp: 3188860157   SSRC: 3194950522

PID Details - Unique PIDs: 58, (10 shown by packet count)
----------------
TS PID: 8191    Packet Count: 184466            CC Error Count: 0
TS PID: 5500    Packet Count: 32484             CC Error Count: 0
TS PID: 5600    Packet Count: 31731             CC Error Count: 0
TS PID: 5400    Packet Count: 17151             CC Error Count: 0
TS PID: 5300    Packet Count: 16778             CC Error Count: 0
TS PID: 2322    Packet Count: 11175             CC Error Count: 0
TS PID: 3847    Packet Count: 5215              CC Error Count: 0
TS PID: 2321    Packet Count: 3726              CC Error Count: 0
TS PID: 192     Packet Count: 2462              CC Error Count: 0
TS PID: 50      Packet Count: 2208              CC Error Count: 0

Service Information - Service Count: 8, (5 shown)
----------------
Service 6940: BBC Two HD (BSkyB) - H.264/AVC HD digital television service
Service 6941: BBC One HD (BSkyB) - H.264/AVC HD digital television service
Service 6943: BBC One NI HD (BSkyB) - H.264/AVC HD digital television service
Service 6945: 6945 (BSkyB) - H.264/AVC HD digital television service
Service 6952: CBBC HD (BSkyB) - H.264/AVC HD digital television service

Elements - Selected Program BBC Two HD (ID:6940) (first 5 shown)
----------------
PID: 5500 (H.264 video)
PID: 5502 (MPEG-1 audio)
PID: 5504 (MPEG-2 packetized data privately defined)
PID: 5503 (MPEG-2 packetized data privately defined)
PID: 5501 (MPEG-2 packetized data privately defined)

TeleText Subtitles - decoding from Service ID 6940
----------------
Live Decoding Page 888

party. If he is elected,

```

Just to make your life easier, we auto-build this using AppVeyor - here is how we are doing right now: 

[![Build status](https://ci.appveyor.com/api/projects/status/08dqscip26lr0g1o/branch/master?svg=true)](https://ci.appveyor.com/project/cinegy/tsanalyser/branch/master)

You can check out the latest compiled binary from the master or pre-master code here:

[AppVeyor TSAnalyser Project Builder](https://ci.appveyor.com/project/cinegy/tsanalyser/build/artifacts)

