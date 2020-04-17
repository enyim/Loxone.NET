// ----------------------------------------------------------------------
// <copyright file="Category.cs">
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
    using System.Text.Json;
    using System.Linq;

    public sealed class Control : IEquatable<Control>
    {
        private Transport.Control _innerControl;
        private CategoryCollection categories;
        private RoomCollection rooms;

        public Uuid Uuid => _innerControl.Uuid;

        public string Name => _innerControl.Name;

        public Uuid? RoomUuid => _innerControl.Room;

        private Room _room;

        public Room? Room => _room ??= rooms.Where(p => p.Uuid == RoomUuid.Value).FirstOrDefault();

        public string ControlType => _innerControl.ControlType;

        public Uuid? CategoryUuid => _innerControl.Category;

        private Category _category;

        public Category? Category => _category ??= categories.Where(p => p.Uuid == CategoryUuid.Value).FirstOrDefault();

        public IDictionary<string, JsonElement> ExtensionData => _innerControl.ExtensionData;

        


        internal Control(Transport.Control control, CategoryCollection categories, RoomCollection rooms)
        {
            Contract.Requires(control != null);
            this._innerControl = control;
            this.categories = categories;
            this.rooms = rooms;
        }

        public bool Equals(Control other)
        {
            if (other == null)
            {
                return false;
            }

            return this.Uuid == other.Uuid;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Control);
        }

        public override int GetHashCode()
        {
            return Uuid.GetHashCode();
        }

        public override string ToString()
        {
            return Name ?? Uuid.ToString();
        }
    }
}
