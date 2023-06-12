/* Copyright 2022-2023 Cinegy GmbH.

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

namespace TsAnalyzer.SerializableModels.Settings
{
    public class MetricsSetting
    {
        public const string SectionName = "Metrics";

        public bool Enabled { get; set; } = true;

        public bool ConsoleExporterEnabled { get; set; }

        public bool OpenTelemetryExporterEnabled { get; set; }

        public string OpenTelemetryEndpoint { get; set; } = "http://localhost:4317";

        public int OpenTelemetryPeriodicExportInterval { get; set; } = 10000;
    }
}
