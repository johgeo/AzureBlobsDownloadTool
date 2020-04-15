using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AzureBlobsDownload
{
    class Program
    {
        private static string _connectionString = string.Empty;
        private static string _blobsContainer = "mysitemedia";
        private static string _basePath = @"c:\temp\mysitemedia\";
        private static string _currentLocalPath =  _basePath;
        private static BlobContainerItem[] _availableBlobContainers;
        private static readonly Stopwatch Stopwatch = new Stopwatch();
        private static int _blobCount = 0;
        private static int _skippedCount = 0;
        private static int _downloadCount = 0;

        static async Task Main(string[] args)
        {
            try
            {
                Stopwatch.Start();

                SetConnectionString();

                var blobServiceClient = new BlobServiceClient(_connectionString);

                SetBasePath();

                _availableBlobContainers = blobServiceClient.GetBlobContainers().AsPages().SelectMany(p => p.Values).ToArray();
                SetBlobContainerFolder();

                var blobContainer = blobServiceClient.GetBlobContainerClient(_blobsContainer);
                var blobs = blobContainer.GetBlobs(BlobTraits.All);
                _blobCount = blobs.Count();
                foreach (var blobItem in blobs)
                {
                    _currentLocalPath = Path.Combine(_currentLocalPath, blobItem.Name);
                    var blobClient = blobContainer.GetBlobClient(blobItem.Name);

                    if (DirOrFileAlreadyExists(blobItem))
                        continue;

                    await Download(blobClient);

                    ResetPath();
                    Console.WriteLine($"Downloaded {blobItem.Name}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Console.WriteLine((_downloadCount + _skippedCount) != _blobCount
                ? "Not all blobs seem to have been processed, try run tool again"
                : "All blobs where processed");

            Stopwatch.Stop();
            Console.WriteLine($"Tool took {Stopwatch.Elapsed:g} to finish");
        }

        private static void ResetPath()
        {
            _currentLocalPath = _basePath;
        }

        private static async Task Download(BlobClient blobClient)
        {
            BlobDownloadInfo download = await blobClient.DownloadAsync();

            using (var fs = new FileStream(_currentLocalPath, FileMode.Create, FileAccess.Write))
            {
                download.Content.CopyTo(fs);
                fs.Close();
            }

            _downloadCount++;
        }

        private static bool DirOrFileAlreadyExists(BlobItem blobItem)
        {
            if (File.Exists(_currentLocalPath))
            {
                ResetPath();
                Console.WriteLine($"Skipped {blobItem.Name}");
                _skippedCount++;
                return true;
            }

            var directoryPath = Path.GetDirectoryName(_currentLocalPath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            return false;
        }

        private static void SetBlobContainerFolder()
        {
            Console.WriteLine("Available containers:");
            Console.WriteLine();
            foreach (var blobContainer in _availableBlobContainers)
            {
                Console.WriteLine(blobContainer.Name);
            }
            Console.WriteLine();
            Console.Write($"Set azure blob container, (will default to {_blobsContainer} if field is left empty): ");
            var blobsContainer = Console.ReadLine();
            _blobsContainer = string.IsNullOrWhiteSpace(blobsContainer) ? _blobsContainer : blobsContainer.Trim();
            Console.WriteLine();
        }

        private static void SetBasePath()
        {
            Console.Write($@"Set base path to local folder, (will default to {_basePath} if field is left empty): ");
            var basePath = Console.ReadLine();
            _basePath = string.IsNullOrWhiteSpace(basePath) ? _basePath : basePath.Trim();
            Console.WriteLine();
        }

        private static void SetConnectionString()
        {
            Console.Write("Azure blob storage connection string (EPiServerAzureBlobs from Diagnostics tool): ");
            var connectionString = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException($"connection string cannot be empty");
            }

            if (connectionString.Trim().Last().Equals(';'))
            {
                connectionString = connectionString.Substring(0, connectionString.Length - 1);
            }

            _connectionString = connectionString.Trim();
            Console.WriteLine();
        }
    }
}
