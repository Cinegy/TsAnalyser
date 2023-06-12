/* Copyright 2016-2023 Cinegy GmbH.

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

using System.Diagnostics;
using TsAnalyzer.SerializableModels.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Hosting;
using NLog.Extensions.Logging;
using NLog.LayoutRenderers.Wrappers;
using NLog.Layouts;
using NLog.Targets;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using TsAnalyzer.Helpers;
using ILogger = NLog.ILogger;
using LogLevel = NLog.LogLevel;
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Logs;
using System.Diagnostics.Metrics;
using Cinegy.TsAnalyzer;

namespace TsAnalyzer
{
    internal class Program
    {
        #region Constants

        public const string EnvironmentVarPrefix = "CINEGYTSA";
        public const string DirectoryAppName = "TSAnalyzer";
        private static readonly string ProgramDataConfigFilePath;
        private static readonly string ProgramDataDirectory;
        private static readonly string BaseConfigFilePath;
        private static readonly string WorkingConfigFilePath;
        private static readonly string WorkingDirectory;

        #endregion

        #region Fields

        private static IConfigurationRoot _configRoot;
        private static IHost _host;
        private static List<KeyValuePair<string,object>> _metricsTags = new();
        private static readonly Meter MetricsMeter = new("Cinegy.TsAnalyzer");
        private static readonly ObservableGauge<double> ServiceUptimeGauge;
        private static readonly DateTime StartTime = DateTime.UtcNow;

        #endregion

        #region Constructors

        static Program()
        {
            WorkingDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? string.Empty;

            if (OperatingSystem.IsWindows())
            {
                ProgramDataDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    $"Cinegy\\{DirectoryAppName}");
            }
            else
            {
                ProgramDataDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    $"Cinegy/{DirectoryAppName}");
            }

#if DEBUG
            const string configFilename = "appsettings.Development.json";
#else
            const string configFilename = "appsettings.json";
#endif

            BaseConfigFilePath = Path.Combine(AppContext.BaseDirectory, configFilename);
            WorkingConfigFilePath = Path.Combine(WorkingDirectory, configFilename);
            ProgramDataConfigFilePath = Path.Combine(ProgramDataDirectory, configFilename);  
            
            ServiceUptimeGauge = MetricsMeter.CreateObservableGauge("tsAnalyzerUptime", () => new Measurement<double>(DateTime.UtcNow.Subtract(StartTime).TotalSeconds, _metricsTags), "sec");
        }

        #endregion

        #region Static members

        public static void Main(string[] args)
        {
            Console.CancelKeyPress += async delegate {
                await _host?.StopAsync();
                await _host?.WaitForShutdownAsync();
            };

            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            var bufferedWarnings = PrepareConfigFile();
            _configRoot = LoadConfiguration(ProgramDataConfigFilePath, args);
            var logger = InitializeLogger();

            logger.Info("----------------------------------------");
            logger.Info($"{Product.Name}: {Product.Version} (Built: {Product.BuildTime})");
            logger.Info($"Executable directory: {WorkingDirectory}");
            logger.Info($"Operating system: {Environment.OSVersion.Platform} ({Environment.OSVersion.VersionString})");
            logger.Info($"Application data directory: {ProgramDataDirectory}");
            
            _metricsTags.Add(new KeyValuePair<string, object>("ProductVersion", Product.Version));
            _metricsTags.Add(new KeyValuePair<string, object>("OS", $"{Environment.OSVersion.Platform}({Environment.OSVersion.VersionString})"));

            // since the logger was not available during the initial config file prep, log anything that got queued for display
            foreach (var bufferedWarning in bufferedWarnings)
            {
                logger.Warn(bufferedWarning);
            }

            try
            {
                logger.Info($"Configuration running from {ProgramDataConfigFilePath}");

                _host = CreateHostBuilder(args, logger).Build();

                _host.Run();
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Stopped program because of exception");
                throw;
            }
            finally
            {
                // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
                LogManager.Shutdown();
            }

        }

        [RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.Get<T>()")]
        private static IHostBuilder CreateHostBuilder(string[] args, ILogger logger)
        {
            var config = _configRoot.Get<AppConfig>();
            
            _metricsTags.Add(new KeyValuePair<string, object>("Ident", config.Ident));

            if (!string.IsNullOrWhiteSpace(config.Label))
            {
                _metricsTags.Add(new KeyValuePair<string, object>("Label", config.Label));
            }
            
            var hostnameVar = Environment.GetEnvironmentVariable($"{EnvironmentVarPrefix}_Hostname");
            if (!string.IsNullOrWhiteSpace(hostnameVar))
            {
                _metricsTags.Add(new KeyValuePair<string, object>("Hostname", hostnameVar));
            }

            var telemetryInstanceId = Guid.NewGuid();
            
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(configHost => { configHost.AddConfiguration(_configRoot); })
                .ConfigureServices((hostContext, services) =>
                {
                    if (config.Metrics?.Enabled == true && !Sdk.SuppressInstrumentation)
                    {
                        logger.Log(LogLevel.Info,$"Metrics enabled - tagged with instance ID: {telemetryInstanceId}");

                        services.AddOpenTelemetry().WithMetrics(builder =>
                        {
                            builder
                                .AddMeter("Cinegy.TsAnalyzer")
                                .AddMeter("Cinegy.TsDecoder")
                                .AddMeter("Cinegy.TsAnalysis")
                                .AddMeter($"Cinegy.TsAnalyzer.{nameof(AnalysisService)}")
                                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(Product.TracingName, config.Ident, Product.Version,false, telemetryInstanceId.ToString()));

                            if (config.Metrics.ConsoleExporterEnabled)
                            {
                                logger.Log(LogLevel.Warn,"Console metrics enabled - only recommended during debugging metrics issues...");
                                builder.AddConsoleExporter();
                            }

                            if (config.Metrics.OpenTelemetryExporterEnabled)
                            {
                                logger.Log(LogLevel.Info,$"OpenTelemetry metrics exporter enabled, using endpoint: {config.Metrics.OpenTelemetryEndpoint}");
                                builder.AddOtlpExporter((o, m) => {
                                    o.Endpoint = new Uri(config.Metrics.OpenTelemetryEndpoint);
                                    m.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = config.Metrics.OpenTelemetryPeriodicExportInterval;
                                });
                            }
                        });
                    }

                    services.AddHostedService<AnalysisService>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddOpenTelemetry(builder =>
                    {
                        builder.IncludeFormattedMessage = true;
                        builder.IncludeScopes = true;
                        builder.ParseStateValues = true;
                        builder.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(Product.TracingName, config.Ident, Product.Version,false, telemetryInstanceId.ToString()));

                        //if (config.Metrics?.ConsoleExporterEnabled == true)
                        //{
                        //    builder.AddConsoleExporter();
                        //}

                        if (config.Metrics?.OpenTelemetryExporterEnabled == true)
                        {
                            builder.AddOtlpExporter(o => {
                                o.Endpoint = new Uri(config.Metrics.OpenTelemetryEndpoint);
                            });
                        }
                    });
                })
                .UseNLog();
        }

        private static List<string> PrepareConfigFile()
        {
            var bufferedLogWarnMessages = new List<string>();
            if (ProgramDataConfigFilePath != null && File.Exists(ProgramDataConfigFilePath))
            {
                if (File.Exists(WorkingConfigFilePath) && File.GetLastWriteTime(WorkingConfigFilePath) > File.GetLastWriteTime(ProgramDataConfigFilePath))
                {
                    if (File.Exists(ProgramDataConfigFilePath))
                    {
                        bufferedLogWarnMessages.Add("There was a problem reading the settings file, resetting to defaults");
                        var programDataConfigFolder = Path.GetDirectoryName(ProgramDataConfigFilePath);
                        if (ProgramDataConfigFilePath != null && Directory.Exists(programDataConfigFolder))
                        {
                            var backupFileSettingsName = $"{Path.GetFileName(ProgramDataConfigFilePath)}-backup_{DateTime.UtcNow.ToFileTimeUtc()}";
                            bufferedLogWarnMessages.Add($"Problematic settings file has been copied to: {backupFileSettingsName}");
                            File.Move(ProgramDataConfigFilePath!, Path.Combine(programDataConfigFolder!, backupFileSettingsName));
                        }
                    }

                    bufferedLogWarnMessages.Add($"Performing import of newer settings from '{WorkingConfigFilePath}' file");
                    File.Copy(WorkingConfigFilePath, ProgramDataConfigFilePath);
                }
            }
            else
            {
                if (!Directory.Exists(ProgramDataDirectory))
                    Directory.CreateDirectory(ProgramDataDirectory);

                if (File.Exists(WorkingConfigFilePath))
                {
                    bufferedLogWarnMessages.Add($"Performing initial import of settings from '{WorkingConfigFilePath}' file to {ProgramDataConfigFilePath}");
                    File.Copy(WorkingConfigFilePath, ProgramDataConfigFilePath!);
                }
                else
                {
                    bufferedLogWarnMessages.Add($"Performing import of default settings from '{BaseConfigFilePath}' file");
                    File.Copy(BaseConfigFilePath, ProgramDataConfigFilePath!);
                }
            }
            return bufferedLogWarnMessages;
        }

        private static ILogger InitializeLogger()
        {
            var config = _configRoot.Get<AppConfig>();

            var logger = LogManager.Setup()
                .LoadConfigurationFromSection(_configRoot)
                .GetCurrentClassLogger();

            if (LogManager.Configuration != null)
            {
                return logger;

            }

            LogManager.Configuration = new LoggingConfiguration();
            ConfigurationItemFactory.Default.LayoutRenderers.RegisterDefinition("pad", typeof(PaddingLayoutRendererWrapper));

            var layout = new SimpleLayout
            {
                Text = "${longdate} ${pad:padding=-10:inner=(${level:upperCase=true})} " +
                       "${pad:padding=-20:fixedLength=true:inner=${logger:shortName=true}} " +
                       "${message} ${exception:format=tostring}"
            };

            if (config.LiveConsole)
            {
                Console.WriteLine("LiveConsole mode is enabled, so normal logging is disabled - disable LiveConsole option for troubleshooting!");
            }
            else
            {
                var consoleTarget = new ColoredConsoleTarget
                {
                    UseDefaultRowHighlightingRules = true,
                    DetectConsoleAvailable = true,
                    Layout = layout
                };

                LogManager.Configuration.AddRule(LogLevel.Info, LogLevel.Fatal, consoleTarget,
                    "Microsoft.Hosting.Lifetime");
                LogManager.Configuration.AddRule(LogLevel.Trace, LogLevel.Info, new NullTarget(), "Microsoft.*", true);
                LogManager.Configuration.AddRule(LogLevel.Info, LogLevel.Fatal, consoleTarget);
            }

            LogManager.ReconfigExistingLoggers();
            return LogManager.GetCurrentClassLogger();
        }

        private static IConfigurationRoot LoadConfiguration(string filepath, string[] args)
        {
            var configBuilder = new ConfigurationBuilder();
            var config = configBuilder.AddJsonFile(filepath, false)
                .AddCommandLine(args)
                .AddEnvironmentVariables($"{EnvironmentVarPrefix}_")
                .Build();

            return config;
        }

        #endregion

    }
}
