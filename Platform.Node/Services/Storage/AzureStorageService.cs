﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.WindowsAzure.StorageClient;
using Platform.Messages;
using Platform.Storage.Azure;

namespace Platform.Node.Services.Storage
{
    public sealed class AzureStorageService : IStorageService
    {
        readonly static ILogger Log = LogManager.GetLoggerFor<AzureStorageService>();
        readonly IPublisher _publisher;
        readonly string _connectionString;
        readonly string _container;

        AzureAppendOnlyStore _store;

        public AzureStorageService(string connectionString, string container, IPublisher publisher)
        {
            _connectionString = connectionString;
            _publisher = publisher;
            _container = container;
        }

        public void Handle(SystemMessage.Init message)
        {
            Log.Info("Storage starting");
            try
            {
                _store = new AzureAppendOnlyStore(_connectionString, _container);
                _publisher.Publish(new SystemMessage.StorageWriterInitializationDone());
            }
            catch (Exception ex)
            {
                Application.Exit(ExitCode.Error, "Failed to initialize store: " + ex.Message);
            }
        }

        public void Handle(ClientMessage.AppendEvents message)
        {
            _store.Append(message.EventStream, new[] { message.Data });

            Log.Info("Storage service got request");
            message.Envelope(new ClientMessage.AppendEventsCompleted());
        }

        public void Handle(ClientMessage.ImportEvents msg)
        {
            Log.Info("Got import request");
            var watch = Stopwatch.StartNew();
            var count = 0;
            var size = 0;
            var client = StorageExtensions.GetCloudBlobClient(_connectionString);
            var blob = client.GetBlockBlobReference(msg.StagingLocation);
            _store.Append(msg.EventStream, EnumerateStaging(blob).Select(bytes =>
                {
                    count += 1;
                    size += bytes.Length;
                    return bytes;
                }));
            var totalSeconds = watch.Elapsed.TotalSeconds;
            var speed = size / totalSeconds;
            Log.Info("Import {0} in {1}sec: {2} m/s or {3}", count, Math.Round(totalSeconds, 4), Math.Round(count / totalSeconds), FormatEvil.SpeedInBytes(speed));

            msg.Envelope(new ClientMessage.ImportEventsCompleted());
        }

        static IEnumerable<byte[]> EnumerateStaging(CloudBlob blob)
        {
            using (var stream = blob.OpenRead())
            using (var reader = new BinaryReader(stream))
            {
                blob.FetchAttributes();
                var length = blob.Properties.Length;
                while (stream.Position < length)
                {
                    var len = reader.ReadInt32();
                    var data = reader.ReadBytes(len);
                    yield return data;
                }
            }
        }
    }
}
