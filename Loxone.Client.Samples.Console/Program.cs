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
            
            using(var connection = new MiniserverConnection(new LXUri("http", "testminiserver.loxone.com", 7779, 7779), logger))
            {
                connection.Credentials = new TokenCredential(login, password, TokenPermission.Web, default, "Loxone.NET Sample Console");
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

                // Create context
                using (var miniserverContext = new MiniserverContext(logger, structureFile))
                {
                    // Set connection
                    miniserverContext.Connection = connection;
                    miniserverContext.ContextParent = null; // set here your cotroller/internal api (used in Controls - onStateChange, OnCommandResponse)
                    // Add listeners
                    // Global - remains with context
                    miniserverContext.AddEventValueStateChanged((sender, e) =>
                    {
                        foreach (var change in e) Console.WriteLine(change);
                    });

                    miniserverContext.AddEventTextStateChanged((sender, e) =>
                    {
                        foreach (var change in e) Console.WriteLine("Text " + change);
                    });
                    // Control listeners - this will be dropped with connection, however this is preferred
                    miniserverContext.Controls.Where(p => p.GetType() == typeof(Switch) && !p.IsSecured).Cast<Switch>().First().OnStateChange += (c, o) =>
                        {Console.WriteLine($"State changed in {c.Name}"); return Task.CompletedTask; };
                    miniserverContext.Controls.Where(p => p.GetType() == typeof(Switch) && !p.IsSecured).Cast<Switch>().First().OnCommandResponse += (response, status, o) =>
                        {Console.WriteLine($"Response for device, response code {status}"); return Task.CompletedTask;} ;

                    Console.WriteLine($"Structure file loaded.");
                    Console.WriteLine($"  Culture: {miniserverContext.StructureFile.Localization.Culture}");
                    Console.WriteLine($"  Last modified: {miniserverContext.StructureFile.LastModified}");
                    Console.WriteLine($"  Miniserver type: {miniserverContext.StructureFile.MiniserverInfo.MiniserverType}");

                    // Enable notification from miniserver
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
