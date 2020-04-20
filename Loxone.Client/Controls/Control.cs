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
    using System.Threading.Tasks;
    using static Loxone.Client.Transport.TaskUtilities;

    public class Control
    {
        /// <summary>
        /// Register new supported controls
        /// </summary>
        private static readonly Dictionary<string, Func<Control>> _factories = new Dictionary<string, Func<Control>>(1)
        {
            {  "Switch", () => new Switch() },
        };

        protected Control()
        {
        }

        /// <summary>
        /// Init values on control
        /// </summary>
        protected virtual void Initialize() { }

        /// <summary>
        /// Update value of control
        /// </summary>
        /// <param name="state"></param>
        protected virtual void UpdateValueState(ValueState state) { }

        /// <summary>
        /// What to do with state chnage
        /// Purpose:
        /// i.e. subscribe to specific control
        /// </summary>
        public event Func<Control, object,Task> OnStateChange;

        /// <summary>
        /// What to do with response from server
        /// string - message
        /// int - response code
        /// </summary>
        public event Func<string, int, object,Task> OnCommandResponse;

        public Uuid Uuid => InnerControl.Uuid;

        public string Name => InnerControl.Name;

        public bool IsSecured => InnerControl.IsSecured;

        public Room Room { get; internal set; }

        public Category Category { get; internal set; }


        internal protected void Execute(Command command) => command.ExecuteAsync(Context, OnCommandResponse).FireAndForgetSafeAsync(Context.Connection);


        internal void OnValueStateUpdate(ValueState state){
            UpdateValueState(state);
            OnStateChange?.Invoke(this,Context.ContextParent);
        }

        protected string GetStateNameByUuid(Uuid uuid) => InnerControl.States
            .Where(pair => pair.Value == uuid)
            .Select(pair => pair.Key)
            .First();

        protected Uuid GetStateUuidByName(string name) => InnerControl.States[name];

        internal static Control CreateControl(Transport.Control innerControl)
        {
            if (!_factories.TryGetValue(innerControl.ControlType, out var factory)) factory = () => new Control();
            var control = factory();
            control.InnerControl = innerControl;
            return control;
        }

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

        internal MiniserverContext Context { get; set; }

    }
}
