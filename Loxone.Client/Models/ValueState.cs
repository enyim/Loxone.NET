// ----------------------------------------------------------------------
// <copyright file="ValueState.cs">
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

    public readonly struct ValueState
    {
        public Uuid Control { get; }
        public double Value { get; }

        public ValueState(Uuid control, double value)
        {
            this.Control = control;
            this.Value = value;
        }

        public override string ToString() => $"{Control}:{Value}";
    }
}
