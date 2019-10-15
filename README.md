# TwicDownloaderCore
Download TWIC chess archives in batch. Work in windows/Unix/macOS

# Prerequisites
Download and install .net core runtime for your OS here: https://dotnet.microsoft.com/download/dotnet-core/2.2

# Usage
1. Goto https://theweekinchess.com/twic and mark down the Twic number range you want to download.
2. Run dotnet TwicDownloader.dll {fromNumber} {toNumber} //replace the fromNumber and toNumber with the number range you want to download.
3. An Output.pgn file is generated. You can append the games in this file into chessbase.

Last update detection:
1. Create a file last.txt with the fromnumber in it.
2. Run TwicDownloader without the parameters and it will download all files starting from the last update number; and the last.txt file will be automatically updated with the new last update number for next time use.

# License
All source code is licensed under MIT license - http://www.opensource.org/licenses/mit-license.php
