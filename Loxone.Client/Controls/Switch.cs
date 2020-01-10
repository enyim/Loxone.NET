// ----------------------------------------------------------------------
// <copyright file="Switch.cs">
//     Copyright (c) The Loxone.NET Authors.  All rights reserved.
// </copyright>
// <license>
//     Use of this source code is governed by the MIT license that can be
//     found in the LICENSE.txt file.
// </license>
// ----------------------------------------------------------------------

using System.Threading.Tasks;

namespace Loxone.Client.Controls
{
    public class Switch : Control
    {
        private bool? _activeState;

        public bool? Active
        {
            get => _activeState;
            set
            {
                if(_activeState != value) Execute(new Command(Uuid, value.Value ? "on" : "off")); // HACK - should check source of set
                else _activeState = value;
            }
        }

        protected override void UpdateValueState(ValueState state)
        {
            if (GetStateNameByUuid(state.Control) == "active")
            {
                _activeState = state.Value != 0;
            }
        }
    }
}
