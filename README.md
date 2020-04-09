# TwicDownloaderCore
Download TWIC chess archives in batch. Work in windows/Unix/MacOS

# Unix/MacOS
Download and install .net core runtime for your OS here: https://dotnet.microsoft.com/download/dotnet-core/3.1;

Publish it to your specific platform;

# Usage
Last update detection:
1. Create a file last.txt with your last downloaded twic number in it(Goto https://theweekinchess.com/twic find the number).
2. Run TwicDownloader.exe and it will download all files starting from the last downloaded number + 1; and the last.txt file will be automatically updated with the new last update number for next time use.
3. An Output.pgn file is generated. You can append the games in this file into chessbase.

Alternative:
1. Goto https://theweekinchess.com/twic and mark down the Twic number range you want to download.
2. Run Dotnet TwicDownloader.dll {fromNumber} {toNumber} //replace the fromNumber and toNumber with the number range you want to download.
3. An Output.pgn file is generated. You can append the games in this file into chessbase.
# License
All source code is licensed under MIT license - http://www.opensource.org/licenses/mit-license.php
