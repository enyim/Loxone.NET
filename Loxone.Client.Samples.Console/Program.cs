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
    using Microsoft.Extensions.Logging;

    internal class Program
    {
        private const string _miniserverAddress = "http://testminiserver.loxone.com:7779/";
        ILoggerFactory loggerFactory = LoggerFactory.Create(b => b.AddFilter("Microsoft", LogLevel.Warning));

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var connection = new MiniserverConnection(new Uri(_miniserverAddress));
            //using (connection)
            //{
            // Specify Miniserver username and password.
            connection.Credentials = new TokenCredential("web", "web", TokenPermission.Web, default, "Loxone.NET Sample Console");

            Console.WriteLine($"Opening connection to miniserver at {connection.Address}...");
            await connection.OpenAsync(cancellationToken);
            Console.WriteLine($"Connected to Miniserver {connection.MiniserverInfo.SerialNumber}, FW version {connection.MiniserverInfo.FirmwareVersion}");

            // Load cached structure file or download a fresh one if the local file does not exist or is outdated.
            string structureFileName = $"LoxAPP3.{connection.MiniserverInfo.SerialNumber}.json";
            StructureFile structureFile = null;
            if (File.Exists(structureFileName))
            {
                structureFile = await StructureFile.LoadAsync(structureFileName, cancellationToken);
                var lastModified = await connection.GetStructureFileLastModifiedDateAsync(cancellationToken);
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
                structureFile = await connection.DownloadStructureFileAsync(cancellationToken);

                // Save it locally on disk.
                await structureFile.SaveAsync(structureFileName, cancellationToken);
            }

            Console.WriteLine($"Structure file loaded.");
            Console.WriteLine($"  Culture: {structureFile.Localization.Culture}");
            Console.WriteLine($"  Last modified: {structureFile.LastModified}");
            Console.WriteLine($"  Miniserver type: {structureFile.MiniserverInfo.MiniserverType}");

            connection.ValueStateChanged += (sender, e) =>
            {
                foreach (var change in e.ValueStates)
                {
                    Console.WriteLine(change);
                }
            };

            connection.TextStateChanged += (sender, e) =>
            {
                foreach (var change in e.TextStates)
                {
                        //Console.WriteLine(change);
                    }
            };

            var context = new MiniserverContext(structureFile, connection, false, loggerFactory);
            context.Controls.Where(p => p.GetType() == typeof(Switch)).ToList().ForEach(p => p.OnStateChange += (c) => Console.WriteLine(c.Name));
            Console.WriteLine("Enabling status updates...");

            try
            {
                await connection.EnableStatusUpdatesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            await Task.Delay(1000);
            var a = context.Controls.Where(p => p.GetType() == typeof(Switch) && !p.IsSecured).Cast<Switch>().First();
            a.Active = true;

            await Task.Delay(10*1000);
            var b = context.Controls.Where(p => p.GetType() == typeof(Switch) && !p.IsSecured).Cast<Switch>().First();
            b.Active = false;


            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            //}
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
                await new Program().RunAsync(cancellationTokenSource.Token);
            }
            catch (Exception ex) when (!System.Diagnostics.Debugger.IsAttached)
            {
                if (!(ex is OperationCanceledException && cancellationTokenSource.IsCancellationRequested))
                {
                    Console.WriteLine(ex);
                }
            }
        }

    }
}
