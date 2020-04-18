// ----------------------------------------------------------------------
// <copyright file="MiniserverContext.cs">
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
    using Loxone.Client.Controls;
    using Microsoft.Extensions.Logging;

    public class MiniserverContext : IDisposable
    {
        private Dictionary<Uuid, Control> _stateToControl = new Dictionary<Uuid, Control>();
        private MiniserverConnection connection;
        private StructureFile structureFile;

        public MiniserverContext(StructureFile structureFile, ILogger logger, MiniserverConnection connection=null, bool ownsConnection=true)
        {
            Logger = logger;
            SetStructureFile(structureFile, nameof(structureFile), throwOnNull: true);
            if(connection != null) SetConnection(connection, ownsConnection, nameof(connection));
        }


        public ILogger Logger { get; private set; }

        public bool Disposed { get; set; }
        public bool OwnsConnection { get; private set; }
        public ControlCollection Controls { get; } = new ControlCollection();
        public MiniserverConnection Connection
        {
            get
            {
                CheckDisposed();
                return connection;
            }

            set => SetConnection(value, ownsConnection: false, nameof(value));
        }
        public StructureFile StructureFile
        {
            get
            {
                CheckDisposed();
                return structureFile;
            }

            set => SetStructureFile(value, nameof(value), throwOnNull: false);
        }



        private void SetConnection(MiniserverConnection connection, bool ownsConnection, string parameterName)
        {
            if (connection.IsDisposed) throw new ArgumentException(Strings.MiniserverContext_ConnectionDisposed, parameterName);

            DisposeConnection(); //previous connection
            this.connection = connection;
            OwnsConnection = ownsConnection;
            WireEventHandlers();
        }

        private void SetStructureFile(StructureFile structureFile, string parameterName, bool throwOnNull)
        {
            if (structureFile == null && throwOnNull) throw new ArgumentNullException(parameterName);
            this.structureFile = structureFile;
            RebuildControls();
        }

        private void WireEventHandlers()
        {
            if (Connection != null)
            {
                Connection.ValueStateChanged += Connection_ValueStateChanged;
                Connection.TextStateChanged += Connection_TextStateChanged;
            }
        }

        private void UnwireEventHandlers()
        {
            if (Connection != null)
            {
                Connection.ValueStateChanged -= Connection_ValueStateChanged;
                Connection.TextStateChanged -= Connection_TextStateChanged;
            }
        }

        private void Connection_ValueStateChanged(object sender, IReadOnlyList<ValueState> e)
        {
            foreach (var state in e) if (_stateToControl.TryGetValue(state.Control, out var control)) control.OnValueStateUpdate(state);
        }

        private void Connection_TextStateChanged(object sender, IReadOnlyList<TextState> e)
        {
        }

        private void RebuildControls()
        {
            Controls.Clear(StructureFile?.InnerFile?.Controls?.Count);
            _stateToControl.Clear();
            if (StructureFile != null)
            {
                foreach (var controlPair in StructureFile.InnerFile.Controls)
                {
                    var innerControl = controlPair.Value;
                    var control = Control.CreateControl(innerControl);
                    control.Room = StructureFile.Rooms[innerControl.Room.Value];
                    control.Category = StructureFile.Categories[innerControl.Category.Value];
                    control.Context = this;
                    Controls.Add(control);
                    if (innerControl.States != null)
                    {
                        foreach (var statePair in innerControl.States) _stateToControl.Add(statePair.Value, control);
                    }
                }
            }
        }

        private void CheckDisposed()
        {
            if (Disposed) throw new ObjectDisposedException(this.GetType().FullName);
        }

        private void DisposeConnection()
        {
            UnwireEventHandlers();
            if (Connection != null && OwnsConnection)
            {
                Logger.LogDebug("Connection's events will be desposed");
                Connection.Dispose();
                OwnsConnection = false;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed) return;
            if (disposing)
            {
                DisposeConnection();
                Disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
