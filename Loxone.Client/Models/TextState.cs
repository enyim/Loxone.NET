// ----------------------------------------------------------------------
// <copyright file="TextState.cs">
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

    public readonly struct TextState
    {
        public Uuid Control { get; }
        public Uuid Icon { get; }
        public string Text { get; }

        public TextState(Uuid control, Uuid icon, string text)
        {
            this.Control = control;
            this.Icon = icon;
            this.Text = text ?? String.Empty;
        }

        public override string ToString() => $"{Control}:{Text}";

    }
}
