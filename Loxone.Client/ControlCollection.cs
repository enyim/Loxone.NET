// ----------------------------------------------------------------------
// <copyright file="ControlCollection.cs">
//     Copyright (c) The Loxone.NET Authors.  All rights reserved.
// </copyright>
// <license>
//     Use of this source code is governed by the MIT license that can be
//     found in the LICENSE.txt file.
// </license>
// ----------------------------------------------------------------------

namespace Loxone.Client
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;

    public sealed class ControlCollection : IReadOnlyCollection<Control>
    {
        private readonly IDictionary<string, Transport.Control> _innerControls;
        private CategoryCollection categories;
        private RoomCollection rooms;
        

        internal ControlCollection(IDictionary<string, Transport.Control> innerControls, CategoryCollection categories, RoomCollection rooms)
        {
            Contract.Requires(innerControls != null);
            this._innerControls = innerControls;
            this.categories = categories;
            this.rooms = rooms;
        }

        public int Count => _innerControls.Count;

        public IEnumerator<Control> GetEnumerator()
        {
            foreach (var pair in _innerControls)
            {
                yield return new Control(pair.Value, categories, rooms);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
