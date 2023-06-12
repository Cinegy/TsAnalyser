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

using System.Reflection;
using static System.Char;

namespace TsAnalyzer.Helpers;

public static class Product
{
    private static readonly Assembly Assembly;

    private static string _tracingName;

    #region Constructors

    static Product()
    {
        Assembly = Assembly.GetExecutingAssembly();

        var appFile = Path.Combine(AppContext.BaseDirectory, "tsanalyzer.exe");

        if (File.Exists(appFile))
        {
            BuildTime = File.GetCreationTime(appFile);
        }
        else
        {
            var fileLocation = Assembly.Location;

            if (File.Exists(fileLocation)){
                BuildTime = File.GetCreationTime(fileLocation);
            }
        }
    }

    #endregion

    #region Static members

    public static string Name => Assembly == null ? "Unknown Product Name" : Assembly.GetName().Name;

    public static string TracingName
    {
        get
        {
            return _tracingName ??= string.Concat(Name.Where(c => !IsWhiteSpace(c))).ToLowerInvariant();
        }
    }

    public static string Version => Assembly == null ? "0.0" : Assembly.GetName().Version?.ToString();

    public static DateTime BuildTime { get; } = DateTime.MinValue;
    
    #endregion
}