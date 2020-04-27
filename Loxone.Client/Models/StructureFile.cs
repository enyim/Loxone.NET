// ----------------------------------------------------------------------
// <copyright file="StructureFile.cs">
//     Copyright (c) The Loxone.NET Authors.  All rights reserved.
// </copyright>
// <license>
//     Use of this source code is governed by the MIT license that can be
//     found in the LICENSE.txt file.
// </license>
// ----------------------------------------------------------------------

namespace Loxone.Client
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Loxone.Client.Controls;
    using System.Linq;
    using Loxone.Client.Models;

    public sealed class StructureFile
    {
        internal Transport.StructureFile InnerFile { get; }

        public DateTime LastModified
        {
            get
            {
                // Structure file uses local time (miniserver based).
                return DateTime.SpecifyKind(InnerFile.LastModified, DateTimeKind.Local);
            }
        }

        public MiniserverInfo MiniserverInfo { get; }

        public ProjectInfo Project { get; }

        public LocalizationInfo Localization { get; }

        private RoomCollection _rooms;

        public RoomCollection Rooms => _rooms??= new RoomCollection(InnerFile.Rooms);


        private CategoryCollection _categories;

        public CategoryCollection Categories => _categories ??= new CategoryCollection(InnerFile.Categories);

        private List<ControlReadOnly> controls;

        public List<ControlReadOnly> Controls => controls ??= InnerFile.Controls.Values.Select(_ => new ControlReadOnly(_)).ToList();

        private StructureFile(Transport.StructureFile innerFile)
        {
            Contract.Requires(innerFile != null);
            this.InnerFile = innerFile;
            this.MiniserverInfo = new MiniserverInfo(InnerFile.MiniserverInfo);
            this.Project = new ProjectInfo(InnerFile.MiniserverInfo);
            this.Localization = new LocalizationInfo(InnerFile.MiniserverInfo);
        }

        public static StructureFile Parse(string s)
        {
            var transportFile = Transport.Serialization.SerializationHelper.Deserialize<Transport.StructureFile>(s);
            return new StructureFile(transportFile);
        }

        public static async Task<StructureFile> LoadAsync(string fileName, CancellationToken cancellationToken)
        {
            using (var file = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                return await LoadAsync(file, cancellationToken).ConfigureAwait(false);
            }
        }

        public static async Task<StructureFile> LoadAsync(Stream stream, CancellationToken cancellationToken)
        {
            var transportFile = await Transport.Serialization.SerializationHelper.DeserializeAsync<Transport.StructureFile>(
                stream, cancellationToken).ConfigureAwait(false);
            return new StructureFile(transportFile);
        }

        public async Task SaveAsync(string fileName, CancellationToken cancellationToken)
        {
            using (var file = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                await SaveAsync(file, cancellationToken).ConfigureAwait(false);
            }
        }

        public Task SaveAsync(Stream stream, CancellationToken cancellationToken)
            => Transport.Serialization.SerializationHelper.SerializeAsync(stream, InnerFile, cancellationToken);
    }
}
