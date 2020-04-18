// ----------------------------------------------------------------------
// <copyright file="Message.cs">
//     Copyright (c) The Loxone.NET Authors.  All rights reserved.
// </copyright>
// <license>
//     Use of this source code is governed by the MIT license that can be
//     found in the LICENSE.txt file.
// </license>
// ----------------------------------------------------------------------

namespace Loxone.Client.Transport
{
    internal struct Message<TValue>
    {
        public MessageHeader Header { get; }

        public LXResponse<TValue> Response { get; }

        public Message(ref MessageHeader header, LXResponse<TValue> response)
        {
            this.Header = header;
            this.Response = response;
        }
    }
}
