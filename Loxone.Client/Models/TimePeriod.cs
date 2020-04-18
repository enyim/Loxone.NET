// ----------------------------------------------------------------------
// <copyright file="TimePeriod.cs">
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
    using System.Globalization;

    public struct TimePeriod
    {
        public DateTime Start { get; }

        public DateTime End { get; }

        public TimePeriod(DateTime start, DateTime end)
        {
            this.Start = start;
            this.End = end;
        }

        public override string ToString() => string.Format(CultureInfo.CurrentCulture, "{0:m} - {1:m}", Start, End);
    }
}
