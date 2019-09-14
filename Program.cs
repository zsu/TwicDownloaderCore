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
            _logger = LogManager.GetCurrentClassLogger();
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: TwicDownloader fromNumber toNumber");
                return;
            }
            var tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                _configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();
                Directory.CreateDirectory(tempFolder);
                var task = Download(_configuration["Url"], Convert.ToInt32(args[0]), Convert.ToInt32(args[1]), tempFolder);
                task.Wait();
                Unzip(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]), tempFolder);
                CombineFiles(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]), tempFolder);
                _logger.Info($"Download files from {args[0]} to {args[1]} successfully.");
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
        static async Task Download(string url, int fromNumber, int toNumber, string folder)
        {
            var filePattern = _configuration["FileNamePattern"];
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
            for (var i = fromNumber; i <= toNumber; i++)
            {
                var fileName = string.Format(filePattern, i);
                var filePath = Path.Combine(folder, fileName);
                var downloadPath = url.EndsWith("/") ? url + fileName : url + "/" + fileName;
                var handler = new RedirectHandler(new HttpClientHandler());
                using (var client = new System.Net.Http.HttpClient(handler))
                {
                    client.Timeout = new TimeSpan(0, 30, 0);
                    using (var result = await client.GetAsync(downloadPath))
                    {
                        if (result.IsSuccessStatusCode)
                        {
                            using (Stream contentStream = await result.Content.ReadAsStreamAsync(),
                            stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 10000, true))
                            {
                                await contentStream.CopyToAsync(stream);
                            }
                        }
                        else
                        {
                            _logger.Error($"Failed to download file {downloadPath}.{result.ReasonPhrase}.");
                        }

                    }
                }
            }
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
    }
}
