// ----------------------------------------------------------------------
// <copyright file="MessageHeader.cs">
//     Copyright (c) The Loxone.NET Authors.  All rights reserved.
// </copyright>
// <license>
//     Use of this source code is governed by the MIT license that can be
//     found in the LICENSE.txt file.
// </license>
// ----------------------------------------------------------------------

namespace Loxone.Client.Transport
{
    using System;
    using System.Collections.Generic;

    internal struct MessageHeader
    {
        public MessageIdentifier Identifier { get; }

        public int Length { get; }

        private readonly MessageInfoFlags _flags;

        public bool IsLengthEstimated => (_flags & MessageInfoFlags.EstimatedLength) != 0;

        public MessageHeader(ArraySegment<byte> header)
        {
            var h = (IList<byte>)header;
            if (h[0] != 3)
            {
                throw new MiniserverTransportException(Strings.MiniserverTransportException_Message);
            }

            Identifier = (MessageIdentifier)h[1];
            _flags = (MessageInfoFlags)h[2];
            Length = BitConverter.ToInt32(header.Array, header.Offset + 4);
        }
    }
}
