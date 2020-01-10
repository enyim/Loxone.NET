// ----------------------------------------------------------------------
// <copyright file="Control.cs">
//     Copyright (c) The Loxone.NET Authors.  All rights reserved.
// </copyright>
// <license>
//     Use of this source code is governed by the MIT license that can be
//     found in the LICENSE.txt file.
// </license>
// ----------------------------------------------------------------------

namespace Loxone.Client.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class Control
    {
        private static readonly Dictionary<string, Func<Control>> _factories = new Dictionary<string, Func<Control>>(1)
        {
            {  "Switch", () => new Switch() },
        };

        private Transport.Control _innerControl;

        internal Transport.Control InnerControl
        {
            get => _innerControl;
            private set
            {
                _innerControl = value;
                Initialize();
            }
        }

        private MiniserverContext? _context;

        public MiniserverContext? Context { get => _context; internal set => _context = value; }

        public Uuid Uuid => InnerControl.Uuid;

        public string Name => InnerControl.Name;

        public bool IsSecured => InnerControl.IsSecured;

        public Room Room { get; internal set; }

        public Category Category { get; internal set; }

        public Action<Control> OnStateChange; // notify something


        protected Control()
        {
        }

        protected virtual void Initialize()
        {
        }

        protected void Execute(Command command)
        {
            command.Execute(Context); // TODO action on response
        }

        protected virtual void UpdateValueState(ValueState state)
        {
        }

        internal void OnValueStateUpdate(ValueState state){
            UpdateValueState(state);
            OnStateChange?.Invoke(this);
        }

        protected string GetStateNameByUuid(Uuid uuid) => InnerControl.States
            .Where(pair => pair.Value == uuid)
            .Select(pair => pair.Key)
            .First();

        protected Uuid GetStateUuidByName(string name) => InnerControl.States[name];

        internal static Control CreateControl(Transport.Control innerControl)
        {
            if (!_factories.TryGetValue(innerControl.ControlType, out var factory))
            {
                factory = () => new Control();
            }

            var control = factory();
            control.InnerControl = innerControl;
            return control;
        }

    }
}
