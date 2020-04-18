// ----------------------------------------------------------------------
// <copyright file="Program.cs">
//     Copyright (c) The Loxone.NET Authors.  All rights reserved.
// </copyright>
// <license>
//     Use of this source code is governed by the MIT license that can be
//     found in the LICENSE.txt file.
// </license>
// ----------------------------------------------------------------------

namespace Loxone.Client.Samples.Console
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Loxone.Client.Controls;
    using Loxone.Client.Transport;
    using Microsoft.Extensions.Logging;

    internal class Program
    {
        ILoggerFactory loggerFactory = LoggerFactory.Create(b => b.AddFilter("Microsoft", LogLevel.Warning));

        private async Task RunAsync(CancellationTokenSource cts)
        {
            var password = "web";
            var login = "web";

            var logger = loggerFactory.CreateLogger<MiniserverContext>();

            var connection = new MiniserverConnection(new LXUri("http", "testminiserver.loxone.com", 7779,7779),logger);
            MiniserverContext miniserverContext = null;
            connection.Credentials = new TokenCredential(login, password, TokenPermission.Web, default, "Loxone.NET Sample Console");
            try
            {
                Console.WriteLine($"Opening connection to miniserver at {connection.Address}...");
                await connection.OpenAsync(cts.Token);
                Console.WriteLine($"Connected to Miniserver {connection.MiniserverInfo.SerialNumber}, FW version {connection.MiniserverInfo.FirmwareVersion}");

                // Load cached structure file or download a fresh one if the local file does not exist or is outdated.
                string structureFileName = $"LoxAPP3.{connection.MiniserverInfo.SerialNumber}.json";
                StructureFile structureFile = null;
                if (File.Exists(structureFileName))
                {
                    structureFile = await StructureFile.LoadAsync(structureFileName, cts.Token);
                    var lastModified = await connection.GetStructureFileLastModifiedDateAsync(cts.Token);
                    if (lastModified > structureFile.LastModified)
                    {
                        // Structure file cached locally is outdated, throw it away.
                        Console.WriteLine("Cached structure file is outdated.");
                        structureFile = null;
                    }
                }

                if (structureFile == null)
                {
                    // The structure file either did not exist on disk or was outdated. Download a fresh copy from
                    // miniserver right now.
                    Console.WriteLine("Downloading structure file...");
                    structureFile = await connection.DownloadStructureFileAsync(cts.Token);

                    // Save it locally on disk.
                    await structureFile.SaveAsync(structureFileName, cts.Token);
                }

                Console.WriteLine($"Structure file loaded.");
                Console.WriteLine($"  Culture: {structureFile.Localization.Culture}");
                Console.WriteLine($"  Last modified: {structureFile.LastModified}");
                Console.WriteLine($"  Miniserver type: {structureFile.MiniserverInfo.MiniserverType}");

                connection.ValueStateChanged += (sender, e) =>
                {
                    foreach (var change in e) Console.WriteLine(change);
                };

                connection.TextStateChanged += (sender, e) =>
                {
                    foreach (var change in e) Console.WriteLine("Text " + change);
                };

                // Create context
                miniserverContext = new MiniserverContext(structureFile, logger)
                {
                    Connection = connection
                };

                Console.WriteLine("Enabling status updates...");
                await connection.EnableStatusUpdatesAsync(cts.Token);                

                // Random example of command
                await Task.Delay(1000);
                var a = miniserverContext.Controls.Where(p => p.GetType() == typeof(Switch) && !p.IsSecured).Cast<Switch>().First();
                a.Active = true;

                await Task.Delay(10 * 1000);
                var b = miniserverContext.Controls.Where(p => p.GetType() == typeof(Switch) && !p.IsSecured).Cast<Switch>().First();
                b.Active = false;

                // Await close
                await connection.IsClosedAsync();
            }
            catch(Exception ex)
            {
                Console.WriteLine("Program inner: "+ ex);
            }
            finally
            {
                miniserverContext?.Dispose();
            }
        }

        static async Task Main(string[] args)
        {
            var cancellationTokenSource = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("Aborted.");
                cancellationTokenSource.Cancel();
            };

            try
            {
                await new Program().RunAsync(cancellationTokenSource);
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException && cancellationTokenSource.IsCancellationRequested))
                {
                    Console.WriteLine(ex);
                }
            }
        }

    }
}
