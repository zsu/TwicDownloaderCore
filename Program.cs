using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace TwicDownloader
{
    class Program
    {
        private static ILogger _logger;
        private static IConfiguration _configuration;
        static void Main(string[] args)
        {
            string last, fromNumber, toNumber = null;
            string lastFilePath = Path.Combine(Directory.GetCurrentDirectory(), "last.txt");
            _logger = LogManager.GetCurrentClassLogger();
            if (args.Length < 2)
            {
                if (File.Exists(lastFilePath))
                {
                    using (StreamReader sr = new StreamReader(lastFilePath))
                    {
                        last = sr.ReadToEnd();
                    }
                    fromNumber = GetNextSequenceNumber(last);
                }
                else
                {
                    Console.WriteLine("Usage: dotnet TwicDownloader.dll fromNumber toNumber");
                    return;
                }
            }
            else
            {
                fromNumber = args[0];
                toNumber = args[1];
            }
            var tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                _configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();
                Directory.CreateDirectory(tempFolder);
                var task = Download(_configuration["Url"], fromNumber, toNumber, tempFolder);
                task.Wait();
                toNumber = task.Result;
                if (!string.IsNullOrWhiteSpace(toNumber) && Convert.ToInt32(toNumber) >= Convert.ToInt32(fromNumber))
                {
                    Unzip(Convert.ToInt32(fromNumber), Convert.ToInt32(toNumber), tempFolder);
                    CombineFiles(Convert.ToInt32(fromNumber), Convert.ToInt32(toNumber), tempFolder);
                    using (StreamWriter outputFile = new StreamWriter(lastFilePath, false))
                    {
                        outputFile.WriteLine(toNumber);
                    }
                    var targetFilePath = _configuration["TargetPgn"];
                    if(!string.IsNullOrWhiteSpace(targetFilePath))
                    {
                        MergeToTarget(Path.Combine(Directory.GetCurrentDirectory(), "output.pgn"), targetFilePath);
                    }
                    _logger.Info($"Download files from {fromNumber} to {toNumber} successfully.");
                }
                else
                    _logger.Info($"No file was downloaded.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                throw;
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                }
            }
        }
        static async Task<string> Download(string url, string fromNumber, string toNumber, string folder)
        {
            string lastDownloadNumber = null;
            int notFoundCount=0;
            var filePattern = _configuration["FileNamePattern"];
            var maxNotFoundTry= _configuration["MaxNotFoundTry"]??"3";
            //using (var client = new WebClient())
            //{
            //    for (var i = fromNumber; i <= toNumber; i++)
            //    {
            //        var fileName = string.Format(filePattern, i);
            //        var filePath = Path.Combine(folder, fileName);
            //        var downloadPath = url.EndsWith("/") ? url + fileName : url + "/" + fileName;
            //        client.DownloadFile(downloadPath, filePath);
            //    }
            //}
            for (var i = Convert.ToInt32(fromNumber); i <= (string.IsNullOrWhiteSpace(toNumber)?5000:Convert.ToInt32(toNumber)); i++)
            {
                var fileName = string.Format(filePattern, i);
                var filePath = Path.Combine(folder, fileName);
                var downloadPath = url.EndsWith("/") ? url + fileName : url + "/" + fileName;
                var handler = new RedirectHandler(new HttpClientHandler());
                //HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, downloadPath);
                //requestMessage.Headers.Add("User-Agent", @"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:71.0) Gecko/20100101 Firefox/71.0");             
                using (var client = new System.Net.Http.HttpClient(handler))
                {
                    client.Timeout = new TimeSpan(0, 30, 0);
                    client.DefaultRequestHeaders.Add("User-Agent", @"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:71.0) Gecko/20100101 Firefox/71.0");
                    using (var result = await client.GetAsync(downloadPath))
                    {
                        if (result.IsSuccessStatusCode)
                        {
                            using (Stream contentStream = await result.Content.ReadAsStreamAsync(),
                            stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 10000, true))
                            {
                                await contentStream.CopyToAsync(stream);
                            }
                            lastDownloadNumber = i.ToString();
                            notFoundCount = 0;
                        }
                        else if (result.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            notFoundCount++;
                            if (!string.IsNullOrWhiteSpace(toNumber) && toNumber != lastDownloadNumber)
                            {
                                _logger.Error($"Failed to download file {downloadPath}.");
                            }
                            else if (notFoundCount == Convert.ToInt32(maxNotFoundTry))
                                break;
                        }
                        else
                            _logger.Error($"Failed to download file {downloadPath}.");

                    }
                }
            }
            return lastDownloadNumber;
        }
        static void Unzip(int fromNumber, int toNumber, string folder)
        {
            IEnumerable<int> numbers = Enumerable.Range(fromNumber, toNumber - fromNumber + 1);
            Parallel.For(fromNumber, toNumber + 1, i =>
            { Unzip(i, folder); });

        }
        static void Unzip(int number, string folder)
        {
            var filePattern = _configuration["FileNamePattern"];
            var filePath = Path.Combine(folder, string.Format(filePattern, number));
            ZipFile.ExtractToDirectory(filePath, folder, true);
        }
        static void CombineFiles(int fromNumber, int toNumber, string folder)
        {
            var files = Directory.GetFiles(folder, "*.pgn");
            const int chunkSize = 2 * 1024; // 2KB
            using (var output = File.Create("output.pgn"))
            {
                foreach (var file in files)
                {
                    using (var input = File.OpenRead(file))
                    {
                        var buffer = new byte[chunkSize];
                        int bytesRead;
                        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, bytesRead);
                        }
                    }
                }
            }
        }
        static void MergeToTarget(string inputFilePath,string targetFilePath)
        {
            int bufferSize =1024;
            byte[] buffer = new byte[bufferSize];
            using (FileStream output = new FileStream(targetFilePath,FileMode.Append,FileAccess.Write))
            {
                using (FileStream input = File.OpenRead(inputFilePath))
                {
                    for (; ; )
                    {
                        int n = input.Read(buffer, 0, buffer.Length);

                        if (n > 0)
                        {
                            output.Write(buffer, 0, n);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
        }
        static string GetNextSequenceNumber(string number)
        {
            int outNumber;
            if (string.IsNullOrWhiteSpace(number))
                throw new Exception("Sequence number cannot be empty.");
            if (!int.TryParse(number, out outNumber))
            {
                throw new Exception($"Invalid sequence number {number}.");
            }
            return (++outNumber).ToString();
        }
    }
}
