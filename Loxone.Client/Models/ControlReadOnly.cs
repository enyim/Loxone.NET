using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Loxone.Client.Models
{
    public sealed class ControlReadOnly : IEquatable<ControlReadOnly>
    {
        private Transport.Control _innerControl;

        public Uuid Uuid => _innerControl.Uuid;

        public string Name => _innerControl.Name;

        public string ControlType => _innerControl.ControlType;

        public bool IsFavorite => _innerControl.IsFavorite;

        public bool IsSecured => _innerControl.IsSecured;

        public int DefaulRating => _innerControl.DefaultRating;

        public Uuid? Room => _innerControl.Room;

        public Uuid? Category => _innerControl.Category;

        public IDictionary<string, Uuid> States => _innerControl.States;

        public IDictionary<string, JsonElement> ExtensionData => _innerControl.ExtensionData;

    
        internal ControlReadOnly(Transport.Control control)
        {
            this._innerControl = control;
        }

        public bool Equals(ControlReadOnly other)
        {
            if (other == null)
            {
                return false;
            }

            return this.Uuid == other.Uuid;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ControlReadOnly);
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
}
