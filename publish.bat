dotnet publish -r linux-arm /p:ShowLinkerSizeComparison=true 
pushd .\Cinegy.TsAnalyzer\bin\Debug\netcoreapp3.1\linux-arm\publish
scp .\* pi@10.183.2.243:/home/pi/
popd